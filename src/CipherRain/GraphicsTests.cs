using System;
using System.IO;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;
namespace CipherRain;
public static class GraphicsTests
{
    public static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        using var form = new Forms.Form { Width = 1920, Height = 1080, FormBorderStyle = Forms.FormBorderStyle.None, ShowInTaskbar = false, StartPosition = Forms.FormStartPosition.Manual, Location = new(-25000, -25000) };
        Exception? error = null;
        form.Shown += async (_, _) =>
        {
            try
            {
                IntPtr handle = form.Handle;
                await Task.Run(() =>
                {
                    using var renderer = new Renderer(handle, 1920, 1080);
                    var s = Settings.Load();
                    s.titleEnabled = s.bootEffect = s.dejaVu = s.codeBursts = s.lightning = s.displayFailure = s.smallBursts = s.drops = s.superman = false;
                    s.pauseWhenCovered = false;
                    s.Validate();
                    Simulation Field()
                    {
                        var sim = new Simulation(1994);
                        renderer.Prepare(s, sim, 1);
                        for (int i = 0; i < 1200; i++)
                            sim.Advance(1.0 / 60, s);
                        return sim;
                    }
                    var baseline = Field();
                    foreach (double glow in new[] { 0, .5, 1 })
                    {
                        s.glowIntensity = glow;
                        renderer.Draw(baseline, s, 1);
                        renderer.Capture(Path.Combine(folder, $"Glow {glow * 100:0}.png"));
                    }
                    for (int k = 1; k <= 9; k++)
                    {
                        var sim = Field();
                        sim.Trigger(k, s);
                        for (int i = 0; i < 40; i++)
                            sim.Advance(1.0 / 60, s);
                        renderer.Draw(sim, s, 1);
                        renderer.Capture(Path.Combine(folder, $"Effect {k}.png"));
                    }
                    var title = Field();
                    s.titleEnabled = true;
                    title.ReplayTitle(s);
                    for (int i = 0; i < 690; i++)
                        title.Advance(1.0 / 60, s);
                    renderer.Draw(title, s, 1);
                    renderer.Capture(Path.Combine(folder, "Incoming Title.png"));
                    s.titleEnabled = false;
                    title.ReplayBoot();
                    for (int i = 0; i < 100; i++)
                        title.Advance(1.0 / 60, s);
                    renderer.Draw(title, s, 1);
                    renderer.Capture(Path.Combine(folder, "Console Boot.png"));
                    s.usePrivateGlyphs = false;
                    var native = Field();
                    renderer.Draw(native, s, 1);
                    renderer.Capture(Path.Combine(folder, "Native Font.png"));
                });
            }
            catch (Exception e) { error = e; }
            finally { form.Close(); }
        };
        Forms.Application.Run(form);
        if (error != null)
            throw error;
    }
    public static void PreviewSaver(string exe, string output)
    {
        using var form = new Forms.Form { Text = "Cipher Rain · screen saver preview test", Width = 680, Height = 430, StartPosition = Forms.FormStartPosition.CenterScreen };
        System.Diagnostics.Process? process = null;
        var timer = new Forms.Timer { Interval = 6000 };
        form.Shown += (_, _) => { var psi = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false }; psi.ArgumentList.Add("/p"); psi.ArgumentList.Add(form.Handle.ToInt64().ToString()); process = System.Diagnostics.Process.Start(psi); timer.Start(); };
        timer.Tick += (_, _) => { timer.Stop(); File.WriteAllText(output, "Screen saver preview process alive: " + (process != null && !process.HasExited)); form.Close(); };
        form.FormClosed += (_, _) => { timer.Dispose(); };
        Forms.Application.Run(form);
        if (process != null)
        {
            if (!process.WaitForExit(4000))
                process.Kill();
            process.Dispose();
        }
    }
}
