using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
namespace CipherRain;
public sealed class Settings
{
    public int schemaVersion { get; set; } = 1;
    public bool launchAtLogin { get; set; } = false;
    public bool pauseWhenCovered { get; set; } = true;
    public bool usePrivateGlyphs { get; set; } = false;
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? extra
    {
        get; set;
    }
    public bool bootEffect { get; set; } = true;
    public double brightness { get; set; } = 0.92;
    public bool burstRandomPosition { get; set; } = true;
    public double burstSpeed { get; set; } = 1;
    public double burstWidth { get; set; } = 2.2;
    public bool codeBursts { get; set; } = true;
    public double codeDensity { get; set; } = 0.82;
    public double colorVariance { get; set; } = 0.16;
    public bool conserveBattery { get; set; } = true;
    public bool crashBrightFlashes { get; set; } = true;
    public bool crashBursts { get; set; } = true;
    public bool crashCodeFlashes { get; set; } = true;
    public bool crashDejaVu { get; set; } = true;
    public bool crashDrops { get; set; } = true;
    public double crashEventCount { get; set; } = 14;
    public double crashInterval { get; set; } = 180;
    public bool crashLightning { get; set; } = true;
    public bool crashScreenErrors { get; set; } = true;
    public bool crashSmallBursts { get; set; } = true;
    public bool crashSuperman { get; set; } = true;
    public bool dejaVu { get; set; } = true;
    public double dejaVuBars { get; set; } = 3;
    public bool dejaVuBaseColor { get; set; } = false;
    public double dejaVuInterval { get; set; } = 35;
    public bool dejaVuRandomize { get; set; } = false;
    public bool dejaVuRewrite { get; set; } = false;
    public bool dejaVuSolid { get; set; } = false;
    public bool dejaVuSparse { get; set; } = false;
    public double dejaVuSpeed { get; set; } = 1;
    public bool displayFailure { get; set; } = true;
    public double dropCount { get; set; } = 24;
    public double dropNegativeDensity { get; set; } = 0.08;
    public double dropPositiveDensity { get; set; } = 0.9;
    public bool drops { get; set; } = true;
    public double dropsInterval { get; set; } = 45;
    public double effectInterval { get; set; } = 35;
    public double effectStrength { get; set; } = 0.85;
    public double frameRate { get; set; } = 60;
    public double glowIntensity { get; set; } = 0.7;
    public double glowTracers { get; set; } = 0.23;
    public string glyphSet { get; set; } = "Classic";
    public double glyphSize { get; set; } = 10;
    public bool independentEffectTiming { get; set; } = true;
    public bool lightning { get; set; } = false;
    public double lightningInterval { get; set; } = 90;
    public double lightningSpeed { get; set; } = 1;
    public double movementBurstInterval { get; set; } = 70;
    public bool movementBursts { get; set; } = true;
    public double negativeTracers { get; set; } = 0.035;
    public string palette { get; set; } = "Emerald Green";
    public double positiveTracers { get; set; } = 0.18;
    public string presetName { get; set; } = "Balanced";
    public double rotators { get; set; } = 0.22;
    public bool seamlessEffects { get; set; } = true;
    public double smallBurstCount { get; set; } = 5;
    public bool smallBurstFade { get; set; } = true;
    public double smallBurstInterval { get; set; } = 45;
    public bool smallBurstRandomSize { get; set; } = true;
    public double smallBurstSize { get; set; } = 0.22;
    public bool smallBurstSolid { get; set; } = true;
    public double smallBurstSpeed { get; set; } = 1.1;
    public double smallBurstWidth { get; set; } = 0.06;
    public bool smallBursts { get; set; } = true;
    public double speed { get; set; } = 1;
    public double speedVariance { get; set; } = 0.42;
    public bool startWithFullScreen { get; set; } = true;
    public double stillBurstInterval { get; set; } = 90;
    public bool stillBursts { get; set; } = true;
    public double streamDensity { get; set; } = 0.96;
    public bool superman { get; set; } = false;
    public double supermanInterval { get; set; } = 120;
    public double supermanSpeed { get; set; } = 1;
    public double titleDelay { get; set; } = 3;
    public bool titleDoubleSpeed { get; set; } = false;
    public bool titleEnabled { get; set; } = true;
    public bool titleHideTrails { get; set; } = true;
    public double titleHold { get; set; } = 5;
    public double titleInterval { get; set; } = 120;
    public bool titleRepeat { get; set; } = false;
    public string titleText { get; set; } = "CIPHERRAIN\nDIGITAL RAIN\nWINDOWS";
    public bool titleZoom { get; set; } = true;
    public double traceBurstInterval { get; set; } = 90;
    public bool traceBursts { get; set; } = true;
    public double trailLength { get; set; } = 34;
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, IncludeFields = true, PropertyNameCaseInsensitive = true };
    public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CipherRain");
    public static string FilePath => Path.Combine(DataDir, "Settings.json");
    public Settings Clone() => Decode(JsonSerializer.Serialize(this, Json));
    public string Encode() => JsonSerializer.Serialize(this, Json);
    public static Settings Decode(string text)
    {
        if (text.Length > 1000000)
            throw new InvalidDataException("Settings file is too large.");
        var s = JsonSerializer.Deserialize<Settings>(text, Json) ?? new();
        s.Validate();
        return s;
    }
    public static Settings Load()
    {
        try
        {
            return File.Exists(FilePath) ? Decode(File.ReadAllText(FilePath)) : new();
        }
        catch { if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".recovered", true); return new(); }
    }
    public void Save()
    {
        Validate();
        Directory.CreateDirectory(DataDir);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, Encode());
        File.Move(tmp, FilePath, true);
    }
    static double Bound(double n, double lo, double hi, double fallback) => double.IsFinite(n) ? Math.Clamp(n, lo, hi) : fallback;
    public void Validate()
    {
        glyphSize = Bound(glyphSize, 6, 24, 10);
        streamDensity = Bound(streamDensity, 0.25, 1, 0.96);
        codeDensity = Bound(codeDensity, 0.25, 1, 0.82);
        speed = Bound(speed, 0.25, 2.5, 1);
        speedVariance = Bound(speedVariance, 0, 1, 0.42);
        trailLength = Bound(trailLength, 8, 64, 34);
        rotators = Bound(rotators, 0, 0.8, 0.22);
        positiveTracers = Bound(positiveTracers, 0, 1, 0.18);
        negativeTracers = Bound(negativeTracers, 0, 1, 0.035);
        glowTracers = Bound(glowTracers, 0, 1, 0.23);
        glowIntensity = Bound(glowIntensity, 0, 1, 0.7);
        brightness = Bound(brightness, 0.25, 1.2, 0.92);
        colorVariance = Bound(colorVariance, 0, 1, 0.16);
        frameRate = Bound(frameRate, 15, 60, 30);
        effectInterval = Bound(effectInterval, 10, 180, 35);
        effectStrength = Bound(effectStrength, 0.25, 1.5, 0.85);
        dejaVuBars = Bound(dejaVuBars, 1, 12, 3);
        dejaVuSpeed = Bound(dejaVuSpeed, 0.5, 2, 1);
        burstWidth = Bound(burstWidth, 0.5, 8, 2.2);
        burstSpeed = Bound(burstSpeed, 0.5, 2, 1);
        smallBurstCount = Bound(smallBurstCount, 1, 12, 5);
        smallBurstSize = Bound(smallBurstSize, 0.08, 0.5, 0.22);
        smallBurstWidth = Bound(smallBurstWidth, 0.02, 0.3, 0.06);
        smallBurstSpeed = Bound(smallBurstSpeed, 0.5, 2, 1);
        dropCount = Bound(dropCount, 1, 64, 24);
        dropPositiveDensity = Bound(dropPositiveDensity, 0, 1, 0.9);
        dropNegativeDensity = Bound(dropNegativeDensity, 0, 1, 0.08);
        lightningSpeed = Bound(lightningSpeed, 0.5, 2, 1);
        supermanSpeed = Bound(supermanSpeed, 0.5, 2, 1);
        crashEventCount = Bound(crashEventCount, 1, 30, 14);
        titleDelay = Bound(titleDelay, 0, 600, 3);
        titleHold = Bound(titleHold, 2, 20, 5);
        titleInterval = Bound(titleInterval, 30, 600, 120);
        crashInterval = Bound(crashInterval, 5, 600, 60);
        dejaVuInterval = Bound(dejaVuInterval, 5, 600, 60);
        dropsInterval = Bound(dropsInterval, 5, 600, 60);
        effectInterval = Bound(effectInterval, 5, 600, 60);
        lightningInterval = Bound(lightningInterval, 5, 600, 60);
        movementBurstInterval = Bound(movementBurstInterval, 5, 600, 60);
        smallBurstInterval = Bound(smallBurstInterval, 5, 600, 60);
        stillBurstInterval = Bound(stillBurstInterval, 5, 600, 60);
        supermanInterval = Bound(supermanInterval, 5, 600, 60);
        titleInterval = Bound(titleInterval, 5, 600, 60);
        traceBurstInterval = Bound(traceBurstInterval, 5, 600, 60);
        dejaVuBars = Math.Round(dejaVuBars);
        dropCount = Math.Round(dropCount);
        smallBurstCount = Math.Round(smallBurstCount);
        crashEventCount = Math.Round(crashEventCount);
        frameRate = frameRate < 27 ? 24 : frameRate < 45 ? 30 : 60;
        titleText = string.Join("\n", (titleText ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ').Split('\n').Take(3));
        if (titleText.Length > 64)
            titleText = titleText[..64];
        if (!new[] { "Classic", "Narrow", "Terminal", "Operator", "Dense", "Legacy" }.Contains(glyphSet))
            glyphSet = "Classic";
        if (palette == "Matrix Green")
            palette = "Emerald Green";
        if (!new[] { "Emerald Green", "Acid Green", "Ice Blue", "Amber" }.Contains(palette))
            palette = "Emerald Green";
        presetName = (presetName ?? "Custom")[..Math.Min(64, (presetName ?? "Custom").Length)];
    }
    public double Number(string key) => (double)(GetType().GetProperty(key)?.GetValue(this) ?? 0d);
    public bool Flag(string key) => (bool)(GetType().GetProperty(key)?.GetValue(this) ?? false);
    public void Set(string key, object value)
    {
        GetType().GetProperty(key)?.SetValue(this, value);
        presetName = "Custom";
    }
    public static Settings Preset(string name, Settings? preferences = null)
    {
        var s = new Settings { presetName = name };
        // A visual preset must not change this computer's startup or desktop preferences.
        if (preferences != null)
        {
            s.launchAtLogin = preferences.launchAtLogin;
            s.pauseWhenCovered = preferences.pauseWhenCovered;
            s.usePrivateGlyphs = preferences.usePrivateGlyphs;
        }
        if (name == "Cinematic")
        {
            s.glyphSize = 12;
            s.streamDensity = .88;
            s.codeDensity = .76;
            s.speed = .74;
            s.speedVariance = .34;
            s.trailLength = 42;
            s.rotators = .16;
            s.glowIntensity = .86;
            s.frameRate = 30;
            s.lightning = true;
        }
        if (name == "Overclocked")
        {
            s.glyphSet = "Operator";
            s.palette = "Acid Green";
            s.glyphSize = 8;
            s.speed = 1.52;
            s.streamDensity = 1;
            s.codeDensity = .92;
            s.rotators = .44;
            s.glowIntensity = .82;
            s.lightning = true;
        }
        if (name == "Battery Saver")
        {
            s.glyphSize = 14;
            s.streamDensity = .58;
            s.codeDensity = .7;
            s.speed = .68;
            s.trailLength = 24;
            s.glowIntensity = .28;
            s.frameRate = 24;
            s.bootEffect = s.dejaVu = s.codeBursts = s.lightning = s.displayFailure = s.smallBursts = s.drops = s.titleEnabled = false;
        }
        return s;
    }
}
