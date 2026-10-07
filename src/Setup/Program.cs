using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
namespace CipherRainSetup;
static class Program
{
    static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "CipherRain");
    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CipherRain");
    static readonly string Programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs), Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    const string RegPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\CipherRain";
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Contains("--remove"))
            {
                int index = Array.IndexOf(args, "--remove");
                int pid = int.Parse(args[index + 1]);
                try
                {
                    Process.GetProcessById(pid).WaitForExit(15000);
                }
                catch { }
                Remove(args.Contains("--remove-settings"));
                return 0;
            }
            if (args.Contains("--quiet"))
            {
                if (args.Contains("--uninstall"))
                {
                    Stop();
                    LaunchRemoval(false);
                }
                else
                    Install(true, false);
                return 0;
            }
            Application.Run(new SetupForm(args.Contains("--uninstall")));
            return 0;
        }
        catch (Exception e) { if (!args.Contains("--quiet")) MessageBox.Show(e.Message, "Cipher Rain Setup", MessageBoxButtons.OK, MessageBoxIcon.Error); File.WriteAllText(Path.Combine(Path.GetTempPath(), "CipherRain Setup Error.txt"), e.ToString()); return 1; }
    }
    static void Stop()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", "CipherRain.Control.v1", PipeDirection.InOut);
            pipe.Connect(1500);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, true, 1024, true);
            writer.WriteLine("quit");
            reader.ReadLine();
            Thread.Sleep(1200);
        }
        catch { }
        foreach (var p in Process.GetProcessesByName("CipherRain"))
        {
            try
            {
                if (p.MainModule?.FileName?.StartsWith(InstallDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (!p.WaitForExit(6000))
                        p.Kill();
                }
            }
            catch { }
            finally { p.Dispose(); }
        }
    }
    static void Install(bool desktopShortcut, bool launch)
    {
        Stop();
        Directory.CreateDirectory(InstallDir);
        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip")!)
        using (var zip = new ZipArchive(resource, ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/'))
                    continue;
                var target = Path.GetFullPath(Path.Combine(InstallDir, entry.FullName));
                if (!target.StartsWith(InstallDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unsafe package path.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, true);
            }
        }
        string self = Environment.ProcessPath!;
        string uninstaller = Path.Combine(InstallDir, "Uninstall Cipher Rain.exe");
        if (!string.Equals(self, uninstaller, StringComparison.OrdinalIgnoreCase))
            File.Copy(self, uninstaller, true);
        string app = Path.Combine(InstallDir, "CipherRain.exe");
        Shortcut(Path.Combine(Programs, "Cipher Rain.lnk"), app, "--start");
        if (desktopShortcut)
            Shortcut(Path.Combine(Desktop, "Cipher Rain.lnk"), app, "--start");
        using (var key = Registry.CurrentUser.CreateSubKey(RegPath))
        {
            key.SetValue("DisplayName", "Cipher Rain");
            key.SetValue("DisplayVersion", "1.0.0");
            key.SetValue("Publisher", "Alexander Pierce");
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("DisplayIcon", app);
            key.SetValue("UninstallString", "\"" + uninstaller + "\" --uninstall");
            key.SetValue("QuietUninstallString", "\"" + uninstaller + "\" --uninstall --quiet");
            key.SetValue("NoModify", 1);
            key.SetValue("NoRepair", 1);
        }
        var manifest = Directory.GetFiles(InstallDir, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(InstallDir, p)).ToArray();
        File.WriteAllText(Path.Combine(InstallDir, "Installed Files.json"), JsonSerializer.Serialize(manifest));
        if (launch)
            Process.Start(new ProcessStartInfo(app, "--start") { UseShellExecute = true });
    }
    static void Shortcut(string path, string target, string arguments)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(t)!;
        try
        {
            dynamic link = shell.CreateShortcut(path);
            link.TargetPath = target;
            link.Arguments = arguments;
            link.WorkingDirectory = InstallDir;
            link.IconLocation = target + ",0";
            link.Description = "Cipher Rain live wallpaper and settings";
            link.Save();
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
        }
        finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
    }
    static void LaunchRemoval(bool removeSettings)
    {
        string temp = Path.Combine(Path.GetTempPath(), "CipherRain Uninstall " + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Environment.ProcessPath!, temp);
        Process.Start(new ProcessStartInfo(temp, $"--remove {Environment.ProcessId}" + (removeSettings ? " --remove-settings" : "")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
    }
    static void Remove(bool removeSettings)
    {
        string manifest = Path.Combine(InstallDir, "Installed Files.json");
        if (File.Exists(manifest))
        {
            var files = JsonSerializer.Deserialize<string[]>(File.ReadAllText(manifest)) ?? Array.Empty<string>();
            foreach (var relative in files.Append("Installed Files.json"))
            {
                string path = Path.GetFullPath(Path.Combine(InstallDir, relative));
                if (!path.StartsWith(InstallDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (File.Exists(path))
                    File.Delete(path);
            }
            foreach (var dir in Directory.GetDirectories(InstallDir, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length))
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            if (!Directory.EnumerateFileSystemEntries(InstallDir).Any())
                Directory.Delete(InstallDir);
        }
        foreach (var link in new[] { Path.Combine(Programs, "Cipher Rain.lnk"), Path.Combine(Desktop, "Cipher Rain.lnk") })
            if (File.Exists(link))
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell")!;
                dynamic shell = Activator.CreateInstance(t)!;
                dynamic shortcut = shell.CreateShortcut(link);
                string target = shortcut.TargetPath;
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                if (target.StartsWith(InstallDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    File.Delete(link);
            }
        using (var run = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
        {
            if ((run?.GetValue("CipherRain") as string)?.Contains(InstallDir, StringComparison.OrdinalIgnoreCase) == true)
                run!.DeleteValue("CipherRain", false);
        }
        using (var desktop = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", true))
        {
            if ((desktop?.GetValue("SCRNSAVE.EXE") as string)?.StartsWith(InstallDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) == true)
            {
                string? prior = null;
                string saved = Path.Combine(DataDir, "PreviousScreenSaver.json");
                if (File.Exists(saved))
                    try
                    {
                        prior = JsonDocument.Parse(File.ReadAllText(saved)).RootElement.GetProperty("path").GetString();
                    }
                    catch { }
                if (string.IsNullOrEmpty(prior))
                    desktop!.DeleteValue("SCRNSAVE.EXE", false);
                else
                    desktop!.SetValue("SCRNSAVE.EXE", prior);
            }
        }
        Registry.CurrentUser.DeleteSubKeyTree(RegPath, false);
        if (removeSettings && Directory.Exists(DataDir))
        {
            string resolved = Path.GetFullPath(DataDir);
            string intended = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CipherRain");
            if (string.Equals(resolved, intended, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(resolved, true);
        }
    }
    sealed class SetupForm : Form
    {
        public SetupForm(bool uninstall)
        {
            Text = uninstall ? "Remove Cipher Rain" : "Install Cipher Rain";
            ClientSize = new(510, 315);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(15, 23, 19);
            ForeColor = Color.FromArgb(232, 243, 236);
            Font = new("Segoe UI", 10);
            using var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("Icon.ico")!;
            Icon = new Icon(icon);
            Controls.Add(new Label { Text = "Cipher Rain", Font = new("Segoe UI", 25, FontStyle.Bold), Bounds = new(28, 23, 450, 50) });
            Controls.Add(new Label { Text = uninstall ? "Remove the app and its desktop shortcut. Your normal wallpaper will remain." : "Live digital rain for your Windows desktop.\nInstalls for your account. No administrator access needed.", Bounds = new(30, 89, 450, 58) });
            var option = new CheckBox { Text = uninstall ? "Keep my appearance settings and presets" : "Create a desktop shortcut named Cipher Rain", Checked = true, Bounds = new(30, 165, 450, 30) };
            Controls.Add(option);
            var button = new Button { Text = uninstall ? "Uninstall" : "Install & open", Bounds = new(280, 235, 190, 42), BackColor = Color.FromArgb(105, 238, 161), ForeColor = Color.FromArgb(12, 35, 21), FlatStyle = FlatStyle.Flat };
            Controls.Add(button);
            var status = new Label { Bounds = new(30, 207, 450, 24), Text = InstallDir, AutoEllipsis = true };
            Controls.Add(status);
            button.Click += async (_, _) => { button.Enabled = false; option.Enabled = false; try { if (uninstall) { await Task.Run(Stop); LaunchRemoval(!option.Checked); Close(); } else { status.Text = "Installing…"; bool shortcut = option.Checked; await Task.Run(() => Install(shortcut, false)); Process.Start(new ProcessStartInfo(Path.Combine(InstallDir, "CipherRain.exe"), "--start") { UseShellExecute = true }); Close(); } } catch (Exception e) { status.Text = "Installation did not finish."; button.Enabled = true; MessageBox.Show(this, e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); } };
        }
    }
}
