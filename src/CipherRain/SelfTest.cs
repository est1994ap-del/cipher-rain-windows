using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
namespace CipherRain;
public static class SelfTest
{
    public static string Run()
    {
        var checks = new List<object>();
        void Assert(string name, bool pass)
        {
            checks.Add(new
            {
                name,
                pass
            });
            if (!pass)
                throw new Exception("Test failed: " + name);
        }
        var defaults = new Settings();
        var settings = Settings.Decode("{\"glyphSize\":999,\"speed\":-2,\"futureOption\":42,\"titleText\":\"a\\nb\\nc\\nd\"}");
        Assert("Settings validation bounds", settings.glyphSize == 24 && settings.speed == .25 && settings.titleText == "a\nb\nc");
        Assert("Unknown fields preserved", settings.Encode().Contains("futureOption"));
        Assert("Legacy palette migration", Settings.Decode("{\"palette\":\"Matrix Green\"}").palette == "Emerald Green");
        foreach (bool startup in new[] { true, false })
        {
            var preferences = new Settings { launchAtLogin = startup, pauseWhenCovered = false, usePrivateGlyphs = true };
            foreach (var name in new[] { "Balanced", "Cinematic", "Overclocked", "Battery Saver" })
            {
                var preset = Settings.Preset(name, preferences);
                Assert($"{name} preserves Windows preferences with startup {startup}",
                    preset.launchAtLogin == startup && !preset.pauseWhenCovered && preset.usePrivateGlyphs && preset.presetName == name);
            }
        }
        var a = new Simulation(123);
        var b = new Simulation(123);
        a.Resize(160, 100, 55, defaults);
        b.Resize(160, 100, 55, defaults);
        for (int i = 0; i < 600; i++)
            a.Advance(1.0 / 60, defaults);
        for (int i = 0; i < 240; i++)
            b.Advance(1.0 / 24, defaults);
        Assert("Same simulation at 24 and 60 fps", a.Cells.SequenceEqual(b.Cells) && Math.Abs(a.Elapsed - b.Elapsed) < 1e-6);
        double elapsed = a.Elapsed;
        var old = a.Cells[0];
        a.Advance(0, defaults);
        Assert("Pause does not advance", a.Elapsed == elapsed && a.Cells[0].Equals(old));
        a.Advance(.5, defaults);
        Assert("Skipped frames preserve elapsed time", Math.Abs(a.Elapsed - elapsed - .5) < 1e-5);
        var e = new Simulation(1);
        e.Resize(100, 80, 55, defaults);
        for (int k = 1; k <= 9; k++)
        {
            e.Trigger(k, defaults);
            Assert("Effect " + Simulation.EffectNames[k] + " available", e.Effects.Any(x => x.Kind == k));
            for (int j = 0; j < 20; j++)
                e.Advance(1.0 / 60, defaults);
            Assert("Effect leaves rain alive " + k, e.Cells.Any(c => c.Ink.W > 0));
        }
        for (int i = 0; i < 100; i++)
            e.Trigger(5, defaults);
        Assert("Effect storage bounded", e.Effects.Count <= 12);
        double before = e.Elapsed;
        e.Resize(120, 95, 45, defaults);
        Assert("Resize preserves simulation clock", e.Elapsed == before && e.Cells.Length == 120 * 95);
        var s = new Settings();
        var t = new TitleSequence("HELLO\nWORLD", 160, 100, 7);
        for (int i = 0; i < 2400; i++)
            t.Advance(1.0 / 60, s);
        Assert("Title naturally finishes", t.Finished(s));
        var seq = Sequences.Lightning(160, 100, 55, ref a.Random);
        Assert("Lightning stored bounded paths", seq.Count >= 16 && seq.Count <= 47 && seq.All(p => p.Length <= 8192 * 4 && p.Length % 4 == 0));
        var temp = Path.Combine(Path.GetTempPath(), "CipherRain-test-" + Guid.NewGuid() + ".json");
        try
        {
            a.SaveCheckpoint(temp);
            var restored = Simulation.LoadCheckpoint(temp);
            Assert("Checkpoint restores field and time", restored != null && restored.Cells.SequenceEqual(a.Cells) && restored.Elapsed == a.Elapsed);
            a.Trigger(7, defaults);
            a.Effects.Last().SourceRows = 0;
            a.SaveCheckpoint(temp);
            Assert("Malformed effect dimensions rejected", Simulation.LoadCheckpoint(temp) == null);
            File.WriteAllText(temp, "{broken");
            Assert("Corrupt checkpoint rejected", Simulation.LoadCheckpoint(temp) == null);
        }
        finally { File.Delete(temp); }
        return JsonSerializer.Serialize(new
        {
            passed = checks.Count,
            checks
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
