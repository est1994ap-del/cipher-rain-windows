using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;
namespace CipherRain;
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception e) { Log.Write("Fatal", e.ToString()); if (args.Contains("--test")) File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test-error.txt"), e.ToString()); else if (!args.Contains("--command")) Forms.MessageBox.Show("Cipher Rain could not start.\n\n" + e.Message + "\n\nDetails are saved in " + Settings.DataDir, "Cipher Rain", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error); return 1; }
    }
    static int Number(string[] args, string key, int fallback = 0)
    {
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int n) ? n : fallback;
    }
    static int Run(string[] args)
    {
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        Forms.Application.EnableVisualStyles();
        if (args.Contains("--graphics-test"))
        {
            GraphicsTests.Run(args[1]);
            return 0;
        }
        if (args.Contains("--preview-saver-test"))
        {
            GraphicsTests.PreviewSaver(Controller.Exe, args[1]);
            return 0;
        }
        if (args.Contains("--test"))
        {
            var report = SelfTest.Run();
            File.WriteAllText(args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "tests.json"), report);
            return 0;
        }
        if (args.Contains("--command"))
        {
            int i = Array.IndexOf(args, "--command");
            string result = Ipc.Send(Ipc.Controller, args[i + 1], 20000).GetAwaiter().GetResult();
            int o = Array.IndexOf(args, "--output");
            if (o >= 0)
                File.WriteAllText(args[o + 1], result);
            return 0;
        }
        if (args.Contains("--host"))
        {
            Forms.Application.Run(new RenderHost(Number(args, "--host"), Number(args, "--parent")));
            return 0;
        }
        string first = args.FirstOrDefault()?.ToLowerInvariant() ?? "";
        if (first.StartsWith("/p") || first.StartsWith("-p"))
        {
            string value = first.Contains(':') ? first.Split(':')[1] : args.ElementAtOrDefault(1) ?? "0";
            if (long.TryParse(value, out long handle) && Native.IsWindow(new(handle)))
                Forms.Application.Run(new RenderHost(0, embedParent: new(handle)));
            return 0;
        }
        if (first == "/s" || first == "-s" || first == "--saver-test")
        {
            try
            {
                Ipc.Send(Ipc.Controller, "saver-enter", 5000).GetAwaiter().GetResult();
            }
            catch { }
            try
            {
                var context = new Forms.ApplicationContext();
                int remaining = Forms.Screen.AllScreens.Length;
                var forms = new RenderHost[remaining];
                for (int i = 0; i < remaining; i++)
                {
                    var form = new RenderHost(i, saver: true, timeout: first == "--saver-test" ? Number(args, "--seconds", 8) : 0);
                    forms[i] = form;
                    form.FormClosed += (_, _) => { foreach (var f in forms) if (f != null && !f.IsDisposed) f.Close(); if (--remaining == 0) context.ExitThread(); };
                    form.Show();
                }
                Forms.Application.Run(context);
            }
            finally { try { Ipc.Send(Ipc.Controller, "saver-leave", 10000).GetAwaiter().GetResult(); } catch { } }
            return 0;
        }
        using var mutex = new Mutex(true, "Local\\CipherRain.Controller.v1", out bool firstInstance);
        if (!firstInstance)
        {
            if (args.Contains("--start"))
                Ipc.Send(Ipc.Controller, "start", 5000).GetAwaiter().GetResult();
            if (!args.Contains("--tray"))
                Ipc.Send(Ipc.Controller, "show", 5000).GetAwaiter().GetResult();
            return 0;
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using var controller = new Controller();
        app.SessionEnding += (_, _) => controller.Window.FlushSettings();
        app.DispatcherUnhandledException += (_, e) => { Log.Write("UI", e.Exception.ToString()); controller.Detail = "An operation could not finish: " + e.Exception.Message; controller.Window.UpdateStatus(); e.Handled = true; };
        app.Startup += async (_, _) => { if (!args.Contains("--tray")) controller.Show(); if (args.Contains("--start")) await controller.Start(); };
        app.Run();
        return 0;
    }
}
