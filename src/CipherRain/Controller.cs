using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
namespace CipherRain;
public sealed class Controller : IDisposable
{
    public Settings Settings = Settings.Load(); public SettingsWindow Window; public string State = "Stopped"; public string Detail = "Your settings are saved automatically.";
    public bool Quitting; public readonly List<Process> Hosts = new(); readonly CancellationTokenSource cancel = new(); readonly Forms.NotifyIcon tray; readonly DispatcherTimer health; bool busy, saverActive, wasPaused; int failures; DateTime lastStart;
    public Controller()
    {
        Window = new(this);
        tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "CipherRain.ico")), Visible = true, Text = "Cipher Rain" };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => Show());
        menu.Items.Add("Start desktop", null, async (_, _) => await Start());
        menu.Items.Add("Pause / Resume", null, async (_, _) => await TogglePause());
        menu.Items.Add("Stop desktop", null, async (_, _) => await Stop());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Screen saver settings", null, (_, _) => OpenSaverSettings());
        menu.Items.Add("Quit Cipher Rain", null, async (_, _) => await Quit());
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Show();
        _ = Ipc.Listen(Ipc.Controller, async cmd => await Application.Current.Dispatcher.InvokeAsync(() => Command(cmd)).Task.Unwrap(), cancel.Token);
        health = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        health.Tick += async (_, _) => { Window.UpdateStatus(); if (!busy && State != "Stopped" && !saverActive) { if (Hosts.Any(p => p.HasExited) || Hosts.Count != Forms.Screen.AllScreens.Length) { if (++failures > 5) { await Stop(); Detail = "Windows could not attach the live desktop. The preview is still available; try Start desktop again."; } else { bool paused = State == "Paused"; await Stop(false); await Start(); if (paused) await TogglePause(); } } else if ((DateTime.UtcNow - lastStart).TotalSeconds > 30) failures = 0; } };
        health.Start();
    }
    public static string Exe => Path.Combine(AppContext.BaseDirectory, "CipherRain.exe");
    public void Show()
    {
        Window.Show();
        Window.WindowState = WindowState.Normal;
        Window.Activate();
    }
    public async Task Start()
    {
        if (busy)
            return;
        if (State != "Stopped" && Hosts.All(p => !p.HasExited))
        {
            await Broadcast("settings:" + Compact(Settings));
            return;
        }
        busy = true;
        try
        {
            foreach (var old in Hosts)
                old.Dispose();
            Hosts.Clear();
            Settings.Save();
            for (int i = 0; i < Forms.Screen.AllScreens.Length; i++)
            {
                var psi = new ProcessStartInfo(Exe) { UseShellExecute = false, CreateNoWindow = true };
                psi.ArgumentList.Add("--host");
                psi.ArgumentList.Add(i.ToString());
                psi.ArgumentList.Add("--parent");
                psi.ArgumentList.Add(Environment.ProcessId.ToString());
                var p = Process.Start(psi);
                if (p != null)
                    Hosts.Add(p);
            }
            State = "Playing";
            Detail = $"Live wallpaper on {Hosts.Count} display{(Hosts.Count == 1 ? "" : "s")}.";
            lastStart = DateTime.UtcNow;
        }
        catch (Exception e) { Detail = "Could not start the desktop: " + e.Message; State = "Stopped"; }
        finally { busy = false; Window.UpdateStatus(); }
    }
    public async Task Stop(bool reset = true)
    {
        if (busy)
            return;
        busy = true;
        try
        {
            foreach (var p in Hosts)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        await Ipc.Send(Ipc.Host(p.Id), "stop", 1500);
                        using var timeout = new CancellationTokenSource(5000);
                        await p.WaitForExitAsync(timeout.Token);
                    }
                }
                catch { try { if (!p.HasExited) p.Kill(); } catch { } }
                p.Dispose();
            }
            Hosts.Clear();
            State = "Stopped";
            Detail = "Desktop stopped. Your normal wallpaper is visible.";
            if (reset)
                failures = 0;
        }
        finally { busy = false; Window.UpdateStatus(); }
    }
    public async Task TogglePause()
    {
        if (State == "Stopped")
            return;
        if (State == "Paused")
        {
            await Broadcast("resume");
            State = "Playing";
            Detail = "The rain continues from where it paused.";
        }
        else
        {
            await Broadcast("pause");
            State = "Paused";
            Detail = "Desktop animation is paused.";
        }
        Window.UpdateStatus();
    }
    public async Task Broadcast(string cmd)
    {
        foreach (var p in Hosts.ToArray())
            try
            {
                if (!p.HasExited)
                    await Ipc.Send(Ipc.Host(p.Id), cmd);
            }
            catch (Exception e) { Log.Write("Host command", e.GetType().Name); }
    }
    public static string Compact(Settings s) => JsonSerializer.Serialize(s);
    public void SavePreferences()
    {
        Settings.Save();
        SetStartup();
    }
    public async Task Apply()
    {
        SavePreferences();
        Window.Preview?.Update(Settings.Clone());
        await Broadcast("settings:" + Compact(Settings));
    }
    public async Task Preview(string cmd)
    {
        if (Window.Preview != null)
            await Window.Preview.Command(cmd);
        await Broadcast(cmd);
        Detail = "Previewing " + (cmd.StartsWith("effect:") ? Simulation.EffectNames[int.Parse(cmd[7..])] : cmd) + ".";
        Window.UpdateStatus();
    }
    public void SetStartup()
    {
        using var run = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
        if (Settings.launchAtLogin)
            run.SetValue("CipherRain", "\"" + Exe + "\" --start --tray");
        else
            run.DeleteValue("CipherRain", false);
    }
    public void OpenSaverSettings()
    {
        Process.Start(new ProcessStartInfo("control.exe", "desk.cpl,,1") { UseShellExecute = true });
    }
    public void RegisterSaver()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CipherRain.scr");
        if (!File.Exists(path))
            File.Copy(Exe, path);
        using var desktop = Registry.CurrentUser.CreateSubKey("Control Panel\\Desktop");
        string? prior = desktop.GetValue("SCRNSAVE.EXE") as string;
        if (prior != path && !File.Exists(Path.Combine(Settings.DataDir, "PreviousScreenSaver.json")))
            File.WriteAllText(Path.Combine(Settings.DataDir, "PreviousScreenSaver.json"), JsonSerializer.Serialize(new
            {
                path = prior
            }));
        desktop.SetValue("SCRNSAVE.EXE", path);
        Detail = "Cipher Rain is selected as your screen saver. Choose its wait time in Windows.";
        OpenSaverSettings();
    }
    public async Task<string> Command(string cmd)
    {
        if (cmd == "show")
            Show();
        else if (cmd == "start")
            await Start();
        else if (cmd == "stop")
            await Stop();
        else if (cmd == "pause" && State == "Playing" || cmd == "resume" && State == "Paused")
            await TogglePause();
        else if (cmd == "quit")
        {
            _ = Task.Run(async () => { await Task.Delay(500); await Application.Current.Dispatcher.InvokeAsync(() => Quit()).Task.Unwrap(); });
            return "OK";
        }
        else if (cmd == "status")
        {
            var hosts = new List<JsonElement>();
            foreach (var p in Hosts)
                try
                {
                    hosts.Add(JsonDocument.Parse(await Ipc.Send(Ipc.Host(p.Id), "metrics")).RootElement.Clone());
                }
                catch { }
            return JsonSerializer.Serialize(new
            {
                state = State,
                detail = Detail,
                hosts,
                preview = Window.Preview == null ? (JsonElement?)null : JsonDocument.Parse(Window.Preview.Metrics()).RootElement.Clone()
            });
        }
        else if (cmd == "saver-enter")
        {
            saverActive = true;
            wasPaused = State == "Paused";
            await Broadcast("pause");
            await Broadcast("checkpoint");
        }
        else if (cmd == "saver-leave")
        {
            await Broadcast("restore");
            if (!wasPaused)
                await Broadcast("resume");
            saverActive = false;
        }
        else if (cmd.StartsWith("set:"))
        {
            Settings = Settings.Decode(cmd[4..]);
            await Apply();
            Window.Rebuild();
        }
        else if (cmd == "hide")
        {
            Window.Hide();
        }
        else if (cmd.StartsWith("capture:"))
        {
            if (Hosts.Count > 0)
                return await Ipc.Send(Ipc.Host(Hosts[0].Id), cmd, 15000);
            if (Window.Preview != null)
                return await Window.Preview.Command(cmd);
        }
        else if (cmd.StartsWith("effect:") || cmd == "title" || cmd == "boot")
            await Preview(cmd);
        else if (cmd == "recover-shell" || cmd == "reset-metrics")
            await Broadcast(cmd);
        return "OK";
    }
    public async Task Quit()
    {
        if (Quitting)
            return;
        Window.FlushSettings();
        Quitting = true;
        await Stop();
        Window.Preview?.Dispose();
        Window.Preview = null;
        tray.Visible = false;
        Application.Current.Shutdown();
    }
    public void Dispose()
    {
        cancel.Cancel();
        health.Stop();
        tray.Dispose();
        cancel.Dispose();
    }
}
