using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Forms = System.Windows.Forms;
namespace CipherRain;
public sealed class RenderLoop : IDisposable
{
    readonly IntPtr hwnd; readonly Thread thread; readonly CancellationTokenSource cancel = new(); readonly AutoResetEvent wake = new(false); readonly ConcurrentQueue<Action> commands = new();
    public volatile bool Paused, Suspended; public volatile Settings Settings; public Simulation Sim; public Renderer? Renderer; public string Error = "", Adapter = ""; public double Fps, Elapsed, MaxGap; public long Frames; public float Scale; public int Width, Height; public bool IsPreview; public string? CheckpointPath;
    readonly double[] intervals = new double[3600]; int intervalCount, intervalIndex; readonly object metricsGate = new();
    public RenderLoop(IntPtr hwnd, int width, int height, float scale, Settings settings, bool preview, string? checkpoint = null)
    {
        this.hwnd = hwnd;
        Width = width;
        Height = height;
        Scale = scale;
        Settings = settings;
        IsPreview = preview;
        CheckpointPath = checkpoint;
        Sim = checkpoint != null ? Simulation.LoadCheckpoint(checkpoint) ?? new Simulation(1994) : new Simulation(1994);
        if (Sim.Elapsed == 0)
            Sim.TitleStarted = settings.titleDelay;
        thread = new(Run)
        {
            IsBackground = true,
            Name = "Cipher Rain graphics"
        };
        thread.Start();
    }
    public void Update(Settings settings)
    {
        commands.Enqueue(() => { var old = Settings; Settings = settings; Sim.Change(old, settings); });
        wake.Set();
    }
    public Task<string> Command(string command)
    {
        var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        commands.Enqueue(() => { try { string result = "OK"; if (command == "pause") Paused = true; else if (command == "resume") Paused = false; else if (command == "title") Sim.ReplayTitle(Settings); else if (command == "boot") Sim.ReplayBoot(); else if (command.StartsWith("effect:")) Sim.Trigger(int.Parse(command[7..]), Settings); else if (command.StartsWith("capture:")) { Renderer!.Draw(Sim, Settings, Scale); Renderer.Capture(command[8..]); Renderer.Present(); } else if (command == "checkpoint") { if (CheckpointPath != null) Sim.SaveCheckpoint(CheckpointPath); } else if (command == "restore") { if (CheckpointPath != null) { var restored = Simulation.LoadCheckpoint(CheckpointPath); if (restored != null) Sim = restored; } } else if (command == "reset-metrics") { lock (metricsGate) { Array.Clear(intervals); intervalCount = intervalIndex = 0; MaxGap = 0; } } else if (command == "metrics") result = Metrics(); done.SetResult(result); } catch (Exception e) { done.SetResult("ERROR: " + e.Message); } });
        wake.Set();
        return done.Task;
    }
    public string Metrics()
    {
        lock (metricsGate)
        {
            var values = intervals.Take(intervalCount).OrderBy(x => x).ToArray();
            double P(double p) => values.Length == 0 ? 0 : values[(int)((values.Length - 1) * p)];
            return JsonSerializer.Serialize(new
            {
                fps = Fps,
                frames = Frames,
                elapsed = Sim.Elapsed,
                width = Width,
                height = Height,
                columns = Sim.Columns,
                rows = Sim.Rows,
                paused = Paused,
                suspended = Suspended,
                adapter = Adapter,
                error = Error,
                window = hwnd.ToInt64(),
                parent = Native.GetParent(hwnd).ToInt64(),
                parentClass = Native.Class(Native.GetParent(hwnd)),
                medianMs = P(.5),
                p95Ms = P(.95),
                p99Ms = P(.99),
                maxMs = MaxGap
            });
        }
    }
    void Run()
    {
        var timer = Native.CreateWaitableTimerEx(IntPtr.Zero, null, 2, 0x1F0003);
        var clock = Stopwatch.StartNew();
        double last = clock.Elapsed.TotalSeconds, measure = last, nextDue = last;
        long measureFrames = 0;
        int failures = 0;
        while (!cancel.IsCancellationRequested)
        {
            try
            {
                Renderer ??= new Renderer(hwnd, Math.Max(1, Width), Math.Max(1, Height));
                Adapter = Renderer.AdapterName;
                while (commands.TryDequeue(out var action))
                    action();
                if (Paused || Suspended || Width < 1 || Height < 1)
                {
                    wake.WaitOne(150);
                    last = clock.Elapsed.TotalSeconds;
                    measure = last;
                    nextDue = last;
                    measureFrames = 0;
                    Fps = 0;
                    continue;
                }
                var s = Settings;
                int fps = (int)s.frameRate;
                if (IsPreview)
                    fps = Math.Min(30, fps);
                if (s.conserveBattery && Native.GetSystemPowerStatus(out var power) && (power.AC == 0 || power.Saver == 1))
                    fps = Math.Min(24, fps);
                double now = clock.Elapsed.TotalSeconds, remaining = nextDue - now;
                if (remaining > .001)
                {
                    if (timer != IntPtr.Zero)
                    {
                        long due = -(long)(remaining * 10000000);
                        Native.SetWaitableTimer(timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, false);
                        Native.WaitForSingleObject(timer, 100);
                    }
                    else
                        wake.WaitOne(Math.Max(1, (int)(remaining * 1000)));
                    continue;
                }
                if (Renderer.FrameReady != IntPtr.Zero)
                    Native.WaitForSingleObject(Renderer.FrameReady, 250);
                now = clock.Elapsed.TotalSeconds;
                double delta = now - last;
                last = now;
                nextDue = Math.Max(nextDue + 1.0 / fps, now);
                Renderer.Resize(Width, Height);
                Renderer.Prepare(s, Sim, Scale);
                Sim.Advance(delta, s);
                Renderer.Draw(Sim, s, Scale);
                Renderer.Present();
                failures = 0;
                Error = "";
                Frames++;
                Elapsed = Sim.Elapsed;
                measureFrames++;
                lock (metricsGate)
                {
                    intervals[intervalIndex++ % intervals.Length] = delta * 1000;
                    intervalCount = Math.Min(intervals.Length, intervalCount + 1);
                    MaxGap = Math.Max(MaxGap, delta * 1000);
                }
                if (now - measure >= 2)
                {
                    Fps = measureFrames / (now - measure);
                    measureFrames = 0;
                    measure = now;
                }
            }
            catch (Exception e) { Error = e.Message; Log.Write("Render", e.ToString()); try { Renderer?.Dispose(); } catch { } Renderer = null; failures++; wake.WaitOne(Math.Min(10000, 500 * failures)); last = clock.Elapsed.TotalSeconds; }
        }
        if (timer != IntPtr.Zero)
            Native.CloseHandle(timer);
        try
        {
            if (CheckpointPath != null)
                Sim.SaveCheckpoint(CheckpointPath);
        }
        catch (Exception e) { Log.Write("Checkpoint", e.GetType().Name); }
        try
        {
            Renderer?.Dispose();
        }
        catch { }
        Renderer = null;
    }
    public void Dispose()
    {
        cancel.Cancel();
        wake.Set();
        thread.Join(4000);
        if (!thread.IsAlive)
        {
            wake.Dispose();
            cancel.Dispose();
        }
    }
}
public sealed class RenderHost : Forms.Form
{
    public RenderLoop? Loop; readonly int monitor, parentPid; readonly bool saver, embedded; readonly IntPtr embedParent; IntPtr desktopParent, powerHandle; readonly Forms.Timer health = new() { Interval = 1000 }; readonly CancellationTokenSource cancel = new(); bool locked, displayOff; Native.POINT mouse; DateTime started = DateTime.UtcNow; readonly int timeout; string monitorName = "";
    public RenderHost(int monitor, int parentPid = 0, bool saver = false, IntPtr embedParent = default, int timeout = 0)
    {
        this.monitor = monitor;
        this.parentPid = parentPid;
        this.saver = saver;
        this.embedParent = embedParent;
        embedded = embedParent != IntPtr.Zero;
        this.timeout = timeout;
        Text = embedded ? "Cipher Rain screen saver preview" : saver ? "Cipher Rain screen saver" : "Cipher Rain wallpaper";
        FormBorderStyle = Forms.FormBorderStyle.None;
        ShowInTaskbar = false;
        BackColor = System.Drawing.Color.Black;
        StartPosition = Forms.FormStartPosition.Manual;
        if (embedded)
        {
            Native.GetClientRect(embedParent, out var r);
            Bounds = new(0, 0, Math.Max(1, r.Width), Math.Max(1, r.Height));
        }
        else
        {
            var screen = Forms.Screen.AllScreens[Math.Clamp(monitor, 0, Forms.Screen.AllScreens.Length - 1)];
            monitorName = screen.DeviceName;
            Bounds = screen.Bounds;
        }
        if (saver && !embedded)
            TopMost = true;
        Shown += (_, _) => Start();
        FormClosed += (_, _) => { health.Stop(); cancel.Cancel(); Native.WTSUnRegisterSessionNotification(Handle); if (powerHandle != IntPtr.Zero) Native.UnregisterPowerSettingNotification(powerHandle); Loop?.Dispose(); };
        health.Tick += (_, _) => Check();
        KeyDown += (_, _) => { if (saver && !embedded) Close(); };
        MouseDown += (_, _) => { if (saver && !embedded) Close(); };
    }
    protected override bool ShowWithoutActivation => !saver || embedded;
    protected override Forms.CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (!saver || embedded)
                cp.ExStyle |= 0x08000000 | 0x80;
            return cp;
        }
    }
    void Start()
    {
        if (embedded)
        {
            Native.SetWindowLongPtr(Handle, -16, new(0x40000000 | 0x10000000));
            Native.SetParent(Handle, embedParent);
        }
        else if (!saver)
        {
            desktopParent = Native.DesktopParent();
            if (!Native.AttachDesktop(Handle, desktopParent, Bounds))
            {
                Log.Write("Desktop", "Desktop surface unavailable. Preview remains available.");
                Close();
                return;
            }
        }
        Native.GetCursorPos(out mouse);
        Native.WTSRegisterSessionNotification(Handle, 0);
        var powerGuid = new Guid("6fe69556-704a-47a0-8f24-c28d936fda47");
        powerHandle = Native.RegisterPowerSettingNotification(Handle, ref powerGuid, 0);
        var settings = Settings.Load();
        string? checkpoint = embedded ? null : Path.Combine(Settings.DataDir, "Continuity-" + monitor + ".json");
        Loop = new(Handle, ClientSize.Width, ClientSize.Height, embedded ? 1 : Native.GetDpiForWindow(Handle) / 96f, settings, embedded, checkpoint);
        if (!saver && !embedded)
            _ = Ipc.Listen(Ipc.Host(Environment.ProcessId), async cmd => { if (cmd == "stop") { BeginInvoke(Close); return "OK"; } if (cmd.StartsWith("settings:")) { Loop.Update(Settings.Decode(cmd[9..])); return "OK"; } if (cmd == "recover-shell") { BeginInvoke(() => { desktopParent = IntPtr.Zero; Check(); }); return "OK"; } return await Loop.Command(cmd); }, cancel.Token);
        health.Start();
    }
    void Check()
    {
        if (parentPid > 0)
            try
            {
                if (Process.GetProcessById(parentPid).HasExited)
                {
                    Close();
                    return;
                }
            }
            catch { Close(); return; }
        if (timeout > 0 && (DateTime.UtcNow - started).TotalSeconds >= timeout)
        {
            Close();
            return;
        }
        if (embedded && !Native.IsWindow(embedParent))
        {
            Close();
            return;
        }
        if (Loop == null)
            return;
        if (saver && !embedded && (DateTime.UtcNow - started).TotalSeconds > 2)
        {
            Native.GetCursorPos(out var p);
            if (Math.Abs(p.X - mouse.X) > 5 || Math.Abs(p.Y - mouse.Y) > 5)
            {
                Close();
                return;
            }
        }
        if (embedded)
        {
            Native.GetClientRect(embedParent, out var r);
            if (ClientSize.Width != r.Width || ClientSize.Height != r.Height)
                Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, r.Width, r.Height, 0x14);
        }
        else if (!saver)
        {
            var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == monitorName);
            if (screen == null)
            {
                Close();
                return;
            }
            Native.GetWindowRect(Handle, out var currentBounds);
            bool boundsChanged = currentBounds.Left != screen.Bounds.Left || currentBounds.Top != screen.Bounds.Top || currentBounds.Width != screen.Bounds.Width || currentBounds.Height != screen.Bounds.Height;
            if (boundsChanged || !Native.IsWindow(desktopParent) || Native.GetParent(Handle) != desktopParent)
            {
                desktopParent = Native.DesktopParent();
                if (!Native.AttachDesktop(Handle, desktopParent, screen.Bounds))
                {
                    Close();
                    return;
                }
            }
            Loop.Suspended = locked || displayOff || (Loop.Settings.pauseWhenCovered && Native.Covered(screen.WorkingArea));
        }
        Loop.Width = ClientSize.Width;
        Loop.Height = ClientSize.Height;
        if (!embedded)
            Loop.Scale = Native.GetDpiForWindow(Handle) / 96f;
    }
    protected override void WndProc(ref Forms.Message m)
    {
        if (m.Msg == 0x21 && !saver)
        {
            m.Result = new(3);
            return;
        }
        if (m.Msg == 0x84 && !saver)
        {
            m.Result = new(-1);
            return;
        }
        if (m.Msg == 0x2B1)
        {
            if (m.WParam.ToInt32() == 7)
            {
                locked = true;
                if (Loop != null)
                {
                    Loop.Suspended = true;
                    _ = Loop.Command("checkpoint");
                }
            }
            if (m.WParam.ToInt32() == 8)
                locked = false;
        }
        if (m.Msg == 0x218)
        {
            if (m.WParam.ToInt32() == 4 && Loop != null)
            {
                Loop.Suspended = true;
                _ = Loop.Command("checkpoint");
            }
            if (m.WParam.ToInt32() == 7 || m.WParam.ToInt32() == 18)
            {
                locked = false;
                displayOff = false;
            }
            if (m.WParam.ToInt32() == 0x8013)
            {
                displayOff = System.Runtime.InteropServices.Marshal.ReadInt32(m.LParam, 20) == 0;
                if (Loop != null)
                    Loop.Suspended = displayOff;
            }
        }
        base.WndProc(ref m);
    }
}
