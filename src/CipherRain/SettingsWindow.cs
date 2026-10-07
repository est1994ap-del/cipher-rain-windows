using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using System.Windows.Automation;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
namespace CipherRain;
public sealed class SettingsWindow : Window
{
    readonly Controller owner; readonly StackPanel page = new(); readonly TextBlock status = new(), detail = new(), previewStats = new(); readonly Grid root = new(); readonly DispatcherTimer saveTimer; readonly Forms.Panel previewPanel = new() { BackColor = System.Drawing.Color.Black }; readonly WindowsFormsHost previewHost; readonly Button pause;
    string active = "Appearance"; bool constructing, sessionLocked, displaySleeping; IntPtr notificationHandle; public RenderLoop? Preview;
    static readonly SolidColorBrush BackgroundBrush = new(Color.FromRgb(13, 18, 17)), CardBrush = new(Color.FromRgb(22, 29, 26)), TextBrush = new(Color.FromRgb(231, 239, 234)), MutedBrush = new(Color.FromRgb(144, 166, 153)), AccentBrush = new(Color.FromRgb(105, 238, 161));
    public SettingsWindow(Controller owner)
    {
        this.owner = owner;
        Title = "Cipher Rain";
        Width = 1140;
        Height = 810;
        MinWidth = 960;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BackgroundBrush;
        Foreground = TextBrush;
        FontFamily = new("Segoe UI");
        FontSize = 13;
        Icon = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "CipherRain.ico")));
        Content = root;
        Resources.Add(typeof(Button), ButtonStyle());
        Resources.Add(typeof(CheckBox), CheckStyle());
        root.Margin = new Thickness(26, 20, 26, 16);
        root.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        root.RowDefinitions.Add(new()
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        root.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        var header = new DockPanel { Margin = new(0, 0, 0, 22) };
        var logo = new Image { Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "CipherRain.png"))), Width = 52, Height = 52, Margin = new(0, 0, 15, 0) };
        header.Children.Add(logo);
        var title = new StackPanel();
        title.Children.Add(Label("Cipher Rain", 28, TextBrush, FontWeights.SemiBold));
        title.Children.Add(Label("DIGITAL RAIN · WINDOWS", 11, MutedBrush));
        header.Children.Add(title);
        root.Children.Add(header);
        var body = new Grid();
        body.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(.48, GridUnitType.Star)
        });
        body.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(.52, GridUnitType.Star)
        });
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        var left = new Grid { Margin = new(0, 0, 24, 0) };
        left.RowDefinitions.Add(new()
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        left.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        body.Children.Add(left);
        var previewCard = new Grid { Background = Brushes.Black };
        previewHost = new WindowsFormsHost { Child = previewPanel };
        previewCard.Children.Add(previewHost);
        left.Children.Add(new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(42, 67, 51)), BorderThickness = new(1), Child = previewCard });
        previewPanel.HandleCreated += (_, _) => { if (Preview == null) { Preview = new(previewPanel.Handle, Math.Max(1, previewPanel.Width), Math.Max(1, previewPanel.Height), .80f, owner.Settings.Clone(), true); } };
        previewPanel.Resize += (_, _) => { if (Preview != null) { Preview.Width = previewPanel.Width; Preview.Height = previewPanel.Height; } };
        var leftBottom = new StackPanel { Margin = new(0, 12, 0, 0) };
        Grid.SetRow(leftBottom, 1);
        left.Children.Add(leftBottom);
        previewStats.Foreground = AccentBrush;
        previewStats.FontSize = 11;
        leftBottom.Children.Add(previewStats);
        leftBottom.Children.Add(Label("Make it your own", 18, TextBrush, FontWeights.SemiBold, new(0, 20, 0, 10)));
        var presets = new UniformGrid { Columns = 2 };
        foreach (var name in new[] { "Balanced", "Cinematic", "Overclocked", "Battery Saver" })
            presets.Children.Add(Button(name, async () => { owner.Settings = Settings.Preset(name, owner.Settings); await owner.Apply(); Rebuild(); }));
        leftBottom.Children.Add(presets);
        leftBottom.Children.Add(Label("Changes appear in the preview and your desktop as you adjust them.", 12, MutedBrush, margin: new(2, 10, 0, 16)));
        var actions = new WrapPanel();
        actions.Children.Add(Button("Start desktop", async () => await owner.Start(), true));
        pause = Button("Pause", async () => await owner.TogglePause());
        actions.Children.Add(pause);
        actions.Children.Add(Button("Stop", async () => await owner.Stop()));
        leftBottom.Children.Add(actions);
        leftBottom.Children.Add(Label("Closing this window keeps Cipher Rain in the notification area.", 11, MutedBrush, margin: new(2, 10, 0, 0)));
        var right = new Grid();
        right.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        right.RowDefinitions.Add(new()
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        Grid.SetColumn(right, 1);
        body.Children.Add(right);
        var nav = new WrapPanel { Margin = new(0, 0, 0, 12) };
        foreach (var tab in new[] { "Appearance", "Effects", "Title & boot", "Windows" })
            nav.Children.Add(Button(tab, () => { active = tab; Rebuild(); }));
        right.Children.Add(nav);
        var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(0, 0, 10, 0) };
        Grid.SetRow(scroll, 1);
        right.Children.Add(scroll);
        var footer = new StackPanel { Margin = new(0, 18, 0, 0) };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        status.Foreground = AccentBrush;
        status.FontWeight = FontWeights.SemiBold;
        detail.Foreground = MutedBrush;
        detail.Margin = new(0, 4, 0, 0);
        detail.TextWrapping = TextWrapping.Wrap;
        footer.Children.Add(status);
        footer.Children.Add(detail);
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        saveTimer.Tick += async (_, _) => { saveTimer.Stop(); try { await owner.Apply(); } catch (Exception e) { owner.Detail = "Settings could not be saved: " + e.Message; } UpdateStatus(); };
        Closing += (_, e) => { if (!owner.Quitting) { FlushSettings(keepLiveUpdate: true); e.Cancel = true; Hide(); } };
        IsVisibleChanged += (_, _) => { if (Preview != null) Preview.Suspended = !IsVisible || sessionLocked || displaySleeping; };
        StateChanged += (_, _) => { if (Preview != null) Preview.Suspended = WindowState == WindowState.Minimized || sessionLocked || displaySleeping; };
        Rebuild();
        UpdateStatus();
    }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this);
        int dark = SystemParameters.HighContrast ? 0 : 1;
        Native.DwmSetWindowAttribute(source.Handle, 20, ref dark, 4);
        Native.WTSRegisterSessionNotification(source.Handle, 0);
        var guid = new Guid("6fe69556-704a-47a0-8f24-c28d936fda47");
        notificationHandle = Native.RegisterPowerSettingNotification(source.Handle, ref guid, 0);
        source.AddHook((IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled) => { if (msg == 0x2B1) { if (w.ToInt32() == 7) sessionLocked = true; if (w.ToInt32() == 8) sessionLocked = false; } if (msg == 0x218) { if (w.ToInt32() == 4) displaySleeping = true; if (w.ToInt32() == 7 || w.ToInt32() == 18) displaySleeping = false; if (w.ToInt32() == 0x8013) displaySleeping = System.Runtime.InteropServices.Marshal.ReadInt32(l, 20) == 0; } if (Preview != null) Preview.Suspended = !IsVisible || WindowState == WindowState.Minimized || sessionLocked || displaySleeping; return IntPtr.Zero; });
    }
    protected override void OnClosed(EventArgs e)
    {
        if (notificationHandle != IntPtr.Zero)
            Native.UnregisterPowerSettingNotification(notificationHandle);
        base.OnClosed(e);
    }
    static Style ButtonStyle()
    {
        var s = new Style(typeof(Button));
        s.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(34, 46, 39))));
        s.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
        s.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(57, 77, 64))));
        s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(13, 8, 13, 8)));
        s.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 7, 7)));
        s.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(13, 8, 13, 8));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        s.Setters.Add(new Setter(Control.TemplateProperty, template));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(49, 73, 58))));
        s.Triggers.Add(hover);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
        s.Triggers.Add(focus);
        return s;
    }
    static Style CheckStyle()
    {
        var s = new Style(typeof(CheckBox));
        s.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
        s.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 6, 0, 6)));
        s.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return s;
    }
    static TextBlock Label(string text, double size = 13, Brush? color = null, FontWeight? weight = null, Thickness? margin = null) => new() { Text = text, FontSize = size, Foreground = color ?? TextBrush, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0) };
    static Button Button(string text, Action action, bool primary = false)
    {
        var b = new Button { Content = text };
        AutomationProperties.SetName(b, text);
        if (primary)
        {
            b.Background = AccentBrush;
            b.Foreground = new SolidColorBrush(Color.FromRgb(8, 32, 18));
            b.FontWeight = FontWeights.SemiBold;
        }
        b.Click += (_, _) => action();
        return b;
    }
    void Changed()
    {
        if (constructing)
            return;
        owner.Settings.presetName = "Custom";
        Preview?.Update(owner.Settings.Clone());
        saveTimer.Stop();
        saveTimer.Start();
    }
    public void FlushSettings(bool keepLiveUpdate = false)
    {
        if (!keepLiveUpdate)
            saveTimer.Stop();
        try { owner.SavePreferences(); }
        catch (Exception e)
        {
            owner.Detail = "Settings could not be saved: " + e.Message;
            Log.Write("Save settings", e.ToString());
            UpdateStatus();
        }
    }
    StackPanel Section(string title, string? description = null)
    {
        var panel = new StackPanel { Margin = new(16) };
        panel.Children.Add(Label(title, 17, TextBrush, FontWeights.SemiBold, new(0, 0, 0, 10)));
        if (description != null)
            panel.Children.Add(Label(description, 12, MutedBrush, margin: new(0, 0, 0, 10)));
        page.Children.Add(new Border { Background = CardBrush, CornerRadius = new(10), BorderBrush = new SolidColorBrush(Color.FromRgb(35, 49, 40)), BorderThickness = new(1), Margin = new(0, 0, 0, 12), Child = panel });
        return panel;
    }
    void Slider(StackPanel p, string text, string key, double min, double max, string suffix = "", bool percent = false)
    {
        var row = new DockPanel { Margin = new(0, 10, 0, 0) };
        var value = Label("", 12, AccentBrush);
        DockPanel.SetDock(value, Dock.Right);
        row.Children.Add(value);
        row.Children.Add(Label(text));
        p.Children.Add(row);
        var slider = new Slider { Minimum = min, Maximum = max, Value = owner.Settings.Number(key), Margin = new(0, 6, 0, 7), SmallChange = (max - min) / 100, LargeChange = (max - min) / 10, TickFrequency = (max - min) / 100 };
        AutomationProperties.SetName(slider, text);
        void Format()
        {
            value.Text = percent ? $"{slider.Value * 100:0}%" : $"{slider.Value:0.##}{suffix}";
        }
        Format();
        slider.ValueChanged += (_, _) => { owner.Settings.Set(key, slider.Value); Format(); Changed(); };
        p.Children.Add(slider);
    }
    void Check(StackPanel p, string text, string key)
    {
        var c = new CheckBox { Content = text, IsChecked = owner.Settings.Flag(key) };
        AutomationProperties.SetName(c, text);
        c.Click += (_, _) => { owner.Settings.Set(key, c.IsChecked == true); Changed(); };
        p.Children.Add(c);
    }
    void Combo(StackPanel p, string text, string key, string[] options)
    {
        p.Children.Add(Label(text, 13, MutedBrush, margin: new(0, 6, 0, 5)));
        var c = new ComboBox { ItemsSource = options, SelectedItem = owner.Settings.GetType().GetProperty(key)!.GetValue(owner.Settings), MinHeight = 32, Margin = new(0, 0, 0, 10), Foreground = Brushes.Black };
        AutomationProperties.SetName(c, text);
        c.SelectionChanged += (_, _) => { if (c.SelectedItem != null) { owner.Settings.Set(key, c.SelectedItem.ToString()!); Changed(); } };
        p.Children.Add(c);
    }
    void Advanced(StackPanel parent, string title, Action<StackPanel> fill)
    {
        var stack = new StackPanel { Margin = new(4, 8, 0, 8) };
        fill(stack);
        parent.Children.Add(new Expander { Header = title, Content = stack, Foreground = MutedBrush, Margin = new(0, 10, 0, 0) });
    }
    public void Rebuild()
    {
        constructing = true;
        page.Children.Clear();
        if (active == "Appearance")
        {
            var look = Section("Look", owner.Settings.usePrivateGlyphs ? "Your Mac appearance is imported. Local reference symbols are available on this computer." : "Sharp symbols with soft, layered light.");
            Combo(look, "Character style", "glyphSet", new[] { "Classic", "Narrow", "Terminal", "Operator", "Dense", "Legacy" });
            Combo(look, "Color palette", "palette", new[] { "Emerald Green", "Acid Green", "Ice Blue", "Amber" });
            var main = Section("Rain & motion");
            Slider(main, "Amount of rain", "streamDensity", .25, 1, percent: true);
            Slider(main, "Fall speed", "speed", .25, 2.5, "×");
            Slider(main, "Trail length", "trailLength", 8, 64, " symbols");
            Slider(main, "Symbol size", "glyphSize", 6, 24, " pt");
            Slider(main, "Glow intensity", "glowIntensity", 0, 1, percent: true);
            Slider(main, "Glowing tracers", "glowTracers", 0, 1, percent: true);
            var advanced = Section("Fine details");
            Slider(advanced, "Brightness", "brightness", .25, 1.2, percent: true);
            Slider(advanced, "Speed variation", "speedVariance", 0, 1, percent: true);
            Slider(advanced, "Symbols within each stream", "codeDensity", .25, 1, percent: true);
            Slider(advanced, "Changing symbols", "rotators", 0, .8, percent: true);
            Slider(advanced, "Bright tracers", "positiveTracers", 0, 1, percent: true);
            Slider(advanced, "Dark tracers", "negativeTracers", 0, 1, percent: true);
            Slider(advanced, "Color variation", "colorVariance", 0, 1, percent: true);
            Check(advanced, "Start with a full rain field", "startWithFullScreen");
        }
        else if (active == "Effects")
        {
            var general = Section("Procedural effects", "Each preview plays over the continuing rain.");
            Check(general, "Keep rain moving through effects", "seamlessEffects");
            Check(general, "Schedule each effect independently", "independentEffectTiming");
            Check(general, "Enable burst effects", "codeBursts");
            Slider(general, "Overall intensity", "effectStrength", .25, 1.5, "×");
            Slider(general, "Shared timing (when independent timing is off)", "effectInterval", 10, 180, " s");
            for (int k = 1; k <= 9; k++)
            {
                int kind = k;
                var effect = Section(Simulation.EffectNames[k]);
                Check(effect, "Enable this effect", Simulation.EffectKeys[k]);
                Slider(effect, "Average time between appearances", Simulation.IntervalKeys[k], 5, 600, " s");
                effect.Children.Add(Button("Preview " + Simulation.EffectNames[k], async () => await owner.Preview("effect:" + kind)));
                Advanced(effect, "Effect options", p => { switch (kind) { case 1: Slider(p, "Bars", "dejaVuBars", 1, 12); Slider(p, "Speed", "dejaVuSpeed", .5, 2, "×"); Check(p, "Rewrite symbols inside bars", "dejaVuRewrite"); Check(p, "Use the base color", "dejaVuBaseColor"); Check(p, "Solid symbols", "dejaVuSolid"); Check(p, "Sparse bars", "dejaVuSparse"); Check(p, "Randomize selected options", "dejaVuRandomize"); break; case 2: case 3: case 4: Slider(p, "Burst width", "burstWidth", .5, 8); Slider(p, "Burst speed", "burstSpeed", .5, 2, "×"); Check(p, "Random position", "burstRandomPosition"); break; case 5: Slider(p, "Number of bursts", "smallBurstCount", 1, 12); Slider(p, "Size", "smallBurstSize", .08, .5, percent: true); Slider(p, "Width", "smallBurstWidth", .02, .3, percent: true); Slider(p, "Speed", "smallBurstSpeed", .5, 2, "×"); Check(p, "Random sizes", "smallBurstRandomSize"); Check(p, "Fade out", "smallBurstFade"); Check(p, "Solid symbols", "smallBurstSolid"); break; case 6: Slider(p, "Number of drops", "dropCount", 1, 64); Slider(p, "Positive density", "dropPositiveDensity", 0, 1, percent: true); Slider(p, "Negative density", "dropNegativeDensity", 0, 1, percent: true); break; case 7: Slider(p, "Lightning speed", "lightningSpeed", .5, 2, "×"); break; case 8: Slider(p, "Sweep speed", "supermanSpeed", .5, 2, "×"); break; case 9: Slider(p, "Events in a sequence", "crashEventCount", 1, 30); foreach (var (key, label) in new[] { ("crashCodeFlashes", "Code flashes"), ("crashBrightFlashes", "Bright flashes"), ("crashDejaVu", "Déjà Vu"), ("crashDrops", "Code drops"), ("crashLightning", "Lightning"), ("crashBursts", "Bursts"), ("crashSmallBursts", "Small bursts"), ("crashScreenErrors", "Display errors"), ("crashSuperman", "Light sweep") }) Check(p, label, key); break; } });
            }
        }
        else if (active == "Title & boot")
        {
            var p = Section("Incoming title", "Up to three lines. Letters arrive independently and settle into your message.");
            Check(p, "Show incoming title", "titleEnabled");
            var text = new TextBox { Text = owner.Settings.titleText, AcceptsReturn = true, MinHeight = 100, MaxLength = 64, Background = BackgroundBrush, Foreground = TextBrush, CaretBrush = AccentBrush, BorderBrush = MutedBrush, Padding = new(10), FontFamily = new("Consolas"), Margin = new(0, 10, 0, 10) };
            AutomationProperties.SetName(text, "Incoming title text");
            text.TextChanged += (_, _) => { owner.Settings.titleText = text.Text; Changed(); };
            p.Children.Add(text);
            p.Children.Add(Button("Preview title", async () => await owner.Preview("title")));
            Slider(p, "Delay after starting", "titleDelay", 0, 120, " s");
            Slider(p, "Hold after arrival", "titleHold", 2, 20, " s");
            Check(p, "Repeat title", "titleRepeat");
            Slider(p, "Time between titles", "titleInterval", 30, 600, " s");
            Check(p, "Double title speed", "titleDoubleSpeed");
            Check(p, "Hide letter trails after arrival", "titleHideTrails");
            Check(p, "Zoom title", "titleZoom");
            var boot = Section("Console boot", "A separate, one-time light sequence. It does not restart the rain.");
            Check(boot, "Play boot effect at startup", "bootEffect");
            boot.Children.Add(Button("Preview boot", async () => await owner.Preview("boot")));
        }
        else
        {
            var p = Section("Playback");
            p.Children.Add(Label("Frame rate", 13, MutedBrush));
            var rate = new ComboBox { ItemsSource = new[] { "24 fps", "30 fps", "60 fps" }, SelectedItem = $"{owner.Settings.frameRate:0} fps", MinHeight = 32, Margin = new(0, 5, 0, 10), Foreground = Brushes.Black };
            rate.SelectionChanged += (_, _) => { owner.Settings.frameRate = double.Parse(rate.SelectedItem.ToString()!.Split(' ')[0]); Changed(); };
            p.Children.Add(rate);
            Check(p, "Conserve battery (cap at 24 fps)", "conserveBattery");
            Check(p, "Pause desktop when covered by a full window", "pauseWhenCovered");
            Check(p, "Start Cipher Rain when I sign in", "launchAtLogin");
            var saver = Section("Screen saver & lock screen", "The screen saver uses the same rain and saved settings. Windows controls secure sign-in; its lock screen cannot display this live animation.");
            saver.Children.Add(Button("Use as Windows screen saver", () => owner.RegisterSaver()));
            saver.Children.Add(Button("Preview screen saver", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Controller.Exe, "/s") { UseShellExecute = true })));
            saver.Children.Add(Button("Export a matching still image", async () => { var dlg = new SaveFileDialog { Filter = "PNG image|*.png", FileName = "Cipher Rain Lock Screen.png" }; if (dlg.ShowDialog(this) == true) { await owner.Command("capture:" + dlg.FileName); owner.Detail = "Still image saved. Select it in Windows Settings → Personalization → Lock screen."; UpdateStatus(); } }));
            var files = Section("Presets & settings");
            files.Children.Add(Button("Export settings", () => { var d = new SaveFileDialog { Filter = "JSON settings|*.json", FileName = "Cipher Rain Settings.json" }; if (d.ShowDialog(this) == true) File.WriteAllText(d.FileName, owner.Settings.Encode()); }));
            files.Children.Add(Button("Import settings", async () => { var d = new OpenFileDialog { Filter = "JSON settings|*.json" }; if (d.ShowDialog(this) == true) try { owner.Settings = Settings.Decode(File.ReadAllText(d.FileName)); await owner.Apply(); Rebuild(); } catch (Exception e) { owner.Detail = "Could not import settings: " + e.Message; UpdateStatus(); } }));
            if (Directory.Exists(Path.Combine(Settings.DataDir, "Private Glyphs")))
                Check(files, "Use my imported Mac reference symbols", "usePrivateGlyphs");
            var info = Section("About Cipher Rain", "Version 1.0 · Built for Windows 11\nNo accounts, ads, telemetry, or internet connection required.\nLive desktop uses a Windows Explorer compatibility layer. If Windows changes it, the normal preview remains available.");
            info.Children.Add(Button("Open local app folder", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Settings.DataDir) { UseShellExecute = true })));
            info.Children.Add(Button("Quit Cipher Rain", async () => await owner.Quit()));
        }
        constructing = false;
    }
    public void UpdateStatus()
    {
        status.Text = owner.State == "Playing" ? "●  DESKTOP PLAYING" : owner.State == "Paused" ? "Ⅱ  DESKTOP PAUSED" : "○  DESKTOP STOPPED";
        detail.Text = owner.Detail;
        pause.Content = owner.State == "Paused" ? "Resume" : "Pause";
        AutomationProperties.SetName(pause, pause.Content.ToString());
        previewStats.Text = Preview == null ? "LIVE PREVIEW" : !string.IsNullOrEmpty(Preview.Error) ? "Preview: " + Preview.Error : $"●  LIVE PREVIEW     {Preview.Fps:0} FPS     {owner.Settings.presetName.ToUpperInvariant()}";
    }
}
