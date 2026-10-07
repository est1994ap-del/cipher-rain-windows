using System;
using System.Linq;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CipherRain;
[StructLayout(LayoutKind.Sequential)]
public struct Cell
{
    public Vector4 Ink, Light;
}
[StructLayout(LayoutKind.Sequential)]
public struct GpuEvent
{
    public Vector4 Location, Parameters, Appearance;
}
[StructLayout(LayoutKind.Sequential)]
public struct BootQuad
{
    public Vector4 Bounds, UV, Light;
}
public struct RainRandom
{
    public ulong State;
    public RainRandom(ulong seed)
    {
        State = seed;
    }
    public ulong Next()
    {
        unchecked
        {
            State += 0x9E3779B97F4A7C15;
            var z = State;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
            return z ^ (z >> 31);
        }
    }
    public double Unit() => (Next() >> 11) / 9007199254740992.0;
    public double Between(double a, double b) => a + Unit() * (b - a);
    public int Int(int n) => (int)(Next() % (ulong)Math.Max(1, n));
}
public sealed class StreamState
{
    public int Column, LastWritten; public double Head, Speed, Variance, Length;
}
public sealed class EffectState
{
    public int Kind, Phase = -1, ChildCount; public float X, Y, Seed; public double Started, Duration, Rate, NextChild;
    public int SourceColumns, SourceRows; public List<int[]>? Paths; public List<uint[]>? Drops; public int[]? DropColumns;
    public bool Child; public Cell[]? Frozen;
    public StormField? Field; public ScreenError? ScreenError; public float[]? Tiles; public double Clock, NextMajor = 6000; public int FlashTick, Total; public bool Finishing; public RainRandom ScheduleRandom;
}
public sealed class Simulation
{
    public int Columns, Rows, GlyphCount = 55;
    public Cell[] Cells = Array.Empty<Cell>();
    public StreamState[] Streams = Array.Empty<StreamState>();
    public List<EffectState> Effects = new();
    public double Elapsed, BootProgress, TitleStarted, NextRotation, NextEffect = 12, Accumulator;
    public bool TitleFinished, PreviewBoot;
    public uint TitleSeed, BootSeed;
    public RainRandom Random;
    public TitleSequence? Title;
    public double[] Deadlines = new double[10];
    public static readonly string[] EffectNames = { "", "Déjà Vu", "Moving burst", "Still burst", "Trace burst", "Small bursts", "Stored code drops", "Lightning", "Light sweep", "System glitch" };
    public static readonly string[] EffectKeys = { "", "dejaVu", "movementBursts", "stillBursts", "traceBursts", "smallBursts", "drops", "lightning", "superman", "displayFailure" };
    public static readonly string[] IntervalKeys = { "", "dejaVuInterval", "movementBurstInterval", "stillBurstInterval", "traceBurstInterval", "smallBurstInterval", "dropsInterval", "lightningInterval", "supermanInterval", "crashInterval" };
    public Simulation() : this(1994) { }
    public Simulation(ulong seed)
    {
        Random = new(seed);
        TitleSeed = Hash((uint)seed ^ 0x7469746c);
        BootSeed = Hash((uint)seed);
        TitleStarted = 3;
    }
    public static uint Hash(uint x)
    {
        unchecked
        {
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            return x ^ (x >> 16);
        }
    }
    public static double Rand(uint x) => (Hash(x) & 0xffffff) / 16777215.0;
    public static uint Seed(int column) => Hash(unchecked((uint)column * 0x9e3779b9 + 0x68bc21eb));
    public static bool Enabled(int kind, Settings s) => s.Flag(EffectKeys[kind]) && (!(kind >= 2 && kind <= 4) || s.codeBursts) && (kind != 9 || ChildChoices(s).Count > 0 || s.crashCodeFlashes || s.crashBrightFlashes || s.crashScreenErrors);
    StreamState MakeStream(int column, Settings s)
    {
        var seed = Seed(column);
        return new()
        {
            Column = column,
            Head = -Random.Between(1, 18),
            Speed = 3.4 + ((seed >> 8) % 8),
            Variance = Rand(seed >> 3) * 2 - 1,
            Length = s.trailLength,
            LastWritten = -100
        };
    }
    public void Resize(int columns, int rows, int glyphs, Settings s)
    {
        int previousGlyphs = GlyphCount;
        GlyphCount = Math.Clamp(glyphs, 1, 1024);
        columns = Math.Clamp(columns, 1, 1280);
        rows = Math.Clamp(rows, 1, 720);
        if (columns * rows > 200000)
            rows = 200000 / columns;
        if (Columns == columns && Rows == rows)
        {
            if (previousGlyphs == GlyphCount)
                return;
            for (int i = 0; i < Cells.Length; i++)
            {
                Cells[i].Ink.X %= GlyphCount;
                Cells[i].Ink.Y %= GlyphCount;
            }
            return;
        }
        var old = Cells;
        var oc = Columns;
        var or = Rows;
        var streams = Streams;
        Columns = columns;
        Rows = rows;
        Cells = new Cell[columns * rows];
        Streams = new StreamState[columns];
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                int i = y * columns + x;
                if (old.Length > 0)
                    Cells[i] = old[Math.Min(or - 1, y * or / rows) * oc + Math.Min(oc - 1, x * oc / columns)];
                else
                {
                    Cells[i].Ink = new(Random.Int(GlyphCount), 0, 1, 0);
                    Cells[i].Ink.Y = Cells[i].Ink.X;
                    Cells[i].Light.W = Random.Unit() < s.codeDensity ? 1 : 0;
                }
                Cells[i].Ink.X %= GlyphCount;
                Cells[i].Ink.Y %= GlyphCount;
            }
        for (int x = 0; x < columns; x++)
        {
            var st = MakeStream(x, s);
            if (streams.Length > 0)
            {
                var prev = streams[Math.Min(streams.Length - 1, x * streams.Length / columns)];
                st.Head = prev.Head * rows / Math.Max(1, or);
            }
            else if (s.startWithFullScreen)
                st.Head = Random.Between(-18, rows + s.trailLength);
            st.LastWritten = (int)st.Head;
            Streams[x] = st;
        }
        foreach (var e in Effects)
            e.Frozen = null;
    }
    public void Change(Settings old, Settings s)
    {
        for (int k = 1; k <= 9; k++)
        {
            if (!Enabled(k, s))
                Deadlines[k] = 0;
            else if (!Enabled(k, old))
                Deadlines[k] = Elapsed + 1.2;
            else if (Deadlines[k] > 0)
                Deadlines[k] = Elapsed + Math.Max(0, Deadlines[k] - Elapsed) * s.Number(IntervalKeys[k]) / Math.Max(5, old.Number(IntervalKeys[k]));
        }
        Effects.RemoveAll(e => e.Kind <= 9 && !e.Child && !Enabled(e.Kind, s));
        if (!s.displayFailure)
            Effects.RemoveAll(e => e.Child || e.Kind == 9);
        else
            Effects.RemoveAll(e => e.Child && !s.Flag(ChildKey(e.Kind)));
        if (Title == null && !TitleFinished && s.titleDelay != old.titleDelay)
            TitleStarted = Elapsed + Math.Max(0, TitleStarted - Elapsed) * (s.titleDelay / Math.Max(.01, old.titleDelay));
        if (s.titleText != old.titleText || (!old.titleEnabled && s.titleEnabled))
            ReplayTitle(s);
        if (!old.bootEffect && s.bootEffect)
            ReplayBoot();
        if (s.seamlessEffects)
            foreach (var e in Effects)
                e.Frozen = null;
    }
    public void ReplayTitle(Settings s)
    {
        TitleStarted = Elapsed;
        TitleFinished = false;
        Title = null;
        TitleSeed = Hash(TitleSeed + 1);
    }
    public void ReplayBoot()
    {
        BootProgress = 0;
        PreviewBoot = true;
        BootSeed = Hash(BootSeed + 1);
    }
    public void Advance(double delta, Settings s)
    {
        if (!double.IsFinite(delta) || delta <= 0 || Cells.Length == 0)
            return;
        Accumulator += Math.Min(10, delta);
        const double step = 1.0 / 60;
        while (Accumulator + 1e-9 >= step)
        {
            Step(step, s);
            Accumulator -= step;
        }
    }
    void Step(double dt, Settings s)
    {
        Elapsed += dt;
        BootProgress = Math.Min(114, BootProgress + dt * s.speed * 20);
        if (s.titleEnabled && !TitleFinished && Elapsed >= TitleStarted && !string.IsNullOrWhiteSpace(s.titleText))
        {
            Title ??= new(s.titleText, Columns, Rows, TitleSeed);
            Title.Advance(dt, s);
            if (Title.Finished(s))
            {
                Title = null;
                if (s.titleRepeat)
                {
                    TitleStarted = Elapsed + Math.Max(10, s.titleInterval);
                    TitleSeed = Hash(TitleSeed + 1);
                }
                else
                    TitleFinished = true;
            }
        }
        Effects.RemoveAll(e => Elapsed - e.Started >= e.Duration);
        var storm = Effects.FirstOrDefault(e => e.Kind == 9);
        if (storm != null)
        {
            for (int k = 1; k < 9; k++)
                if (Deadlines[k] > 0)
                    Deadlines[k] += dt;
            AdvanceStorm(storm, s, dt);
        }
        else
        {
            if (s.independentEffectTiming)
            {
                for (int k = 1; k <= 9; k++)
                    if (Enabled(k, s))
                    {
                        if (Deadlines[k] == 0)
                            Deadlines[k] = Elapsed + Random.Between(.65, 1.35) * s.Number(IntervalKeys[k]);
                        if (Deadlines[k] <= Elapsed && !Effects.Any(e => e.Kind == k))
                        {
                            Trigger(k, s);
                            Deadlines[k] = Elapsed + Random.Between(.65, 1.35) * s.Number(IntervalKeys[k]);
                            break;
                        }
                    }
            }
            else if (Elapsed >= NextEffect && Effects.Count == 0)
            {
                var available = Enumerable.Range(1, 9).Where(k => Enabled(k, s)).ToArray();
                if (available.Length > 0)
                    Trigger(available[Random.Int(available.Length)], s);
                NextEffect = Elapsed + Random.Between(Math.Max(8, s.effectInterval * .55), Math.Max(9, s.effectInterval * 1.55));
            }
        }
        for (int i = 0; i < Cells.Length; i++)
        {
            Cells[i].Ink.W = 0;
            Cells[i].Light.X = 0;
            Cells[i].Light.Z = 0;
            Cells[i].Ink.Z = Math.Min(1, Cells[i].Ink.Z + (float)(dt * 14));
        }
        for (int c = 0; c < Streams.Length; c++)
        {
            var st = Streams[c];
            st.Length = s.trailLength;
            st.Head += dt * st.Speed * (1 + st.Variance * s.speedVariance) * s.speed;
            if (st.Head - st.Length > Rows)
            {
                st = MakeStream(c, s);
                Streams[c] = st;
            }
            int leading = (int)Math.Floor(st.Head), first = Math.Max(0, (int)Math.Floor(st.Head - st.Length)), last = Math.Min(Rows - 1, leading);
            if (leading > st.LastWritten)
            {
                for (int y = Math.Max(0, st.LastWritten + 1); y <= last; y++)
                {
                    int i = y * Columns + c;
                    float glyph = Random.Int(GlyphCount);
                    Cells[i].Ink = new(glyph, glyph, 1, 0);
                    Cells[i].Light.W = Random.Unit() < s.codeDensity ? 1 : 0;
                }
                st.LastWritten = leading;
            }
            var seed = Seed(c);
            if (first > last || Rand(seed >> 1) > s.streamDensity)
                continue;
            float brightness = (float)((Rand(seed >> 17) < s.negativeTracers ? .34 : 1) * (Rand(seed >> 21) < s.positiveTracers ? 1.22 : 1));
            float tint = (float)(Rand(seed >> 11) - .5), glow = Rand(seed >> 25) < s.glowTracers ? 1 : .38f;
            for (int y = first; y <= last; y++)
            {
                int i = y * Columns + c;
                double distance = st.Head - y;
                float intensity = (float)(Math.Pow(Math.Max(0, 1 - distance / st.Length), 1.7) * s.brightness) * brightness;
                if (intensity > Cells[i].Ink.W)
                {
                    Cells[i].Ink.W = intensity * (distance < 1.8 ? 1 : Cells[i].Light.W);
                    Cells[i].Light.X = distance < 1 ? 1 : 0;
                    Cells[i].Light.Y = tint;
                    Cells[i].Light.Z = glow;
                }
            }
        }
        if (Elapsed >= NextRotation)
        {
            NextRotation = Elapsed + .1;
            for (int j = 0; j < (int)(Cells.Length * s.rotators * .12); j++)
            {
                int i = Random.Int(Cells.Length);
                Cells[i].Ink.Y = Cells[i].Ink.X;
                Cells[i].Ink.X = Random.Int(GlyphCount);
                Cells[i].Ink.Z = 0;
            }
        }
        if (s.dejaVuRewrite)
            foreach (var e in Effects.Where(e => e.Kind == 1))
            {
                int phase = (int)((Elapsed - e.Started) * e.Rate * 4);
                if (phase == e.Phase)
                    continue;
                e.Phase = phase;
                var (_, count) = DejaStyle(s, (uint)e.Seed);
                for (int y = 0; y < Rows; y++)
                {
                    bool hit = false;
                    for (int b = 0; b < count; b++)
                    {
                        double center = Math.Floor(Rand((uint)e.Seed + (uint)b * 991 + (uint)phase * 673) * Rows), half = Math.Max(1, Rows * (.035 + Rand((uint)e.Seed + (uint)b * 337 + (uint)phase) * .13));
                        hit |= Math.Abs(y - center) <= half;
                    }
                    if (hit)
                        for (int x = 0; x < Columns; x++)
                        {
                            int i = y * Columns + x;
                            Cells[i].Ink.X = Cells[i].Ink.Y = Random.Int(GlyphCount);
                            Cells[i].Ink.Z = 1;
                        }
                }
            }
    }
    public static (int flags, int count) DejaStyle(Settings s, uint seed)
    {
        bool Ch(bool flag, uint salt) => flag && (!s.dejaVuRandomize || Rand(seed + salt) >= .5);
        bool sparse = Ch(s.dejaVuSparse, 911);
        return ((Ch(s.dejaVuBaseColor, 101) ? 1 : 0) | (Ch(s.dejaVuSolid, 307) ? 2 : 0) | (sparse ? 4 : 0), sparse ? 1 + (int)(seed % 2) : (int)s.dejaVuBars);
    }
    public void Trigger(int kind, Settings s, bool child = false)
    {
        if (Cells.Length == 0 || kind < 1 || kind > 12)
            return;
        if (kind == 9 && ChildChoices(s).Count == 0 && !s.crashCodeFlashes && !s.crashBrightFlashes && !s.crashScreenErrors)
            return;
        if (kind == 6 || kind == 7)
            Effects.RemoveAll(e => e.Kind == kind);
        if (kind == 9)
            Effects.RemoveAll(e => e.Kind == 9 || e.Child);
        if (Effects.Count >= 12)
        {
            int i = Effects.FindIndex(e => e.Kind != 9);
            Effects.RemoveAt(Math.Max(0, i));
        }
        double rate = s.speed * (kind switch
        {
            1 => s.dejaVuSpeed,
            2 or 3 or 4 => s.burstSpeed,
            5 => s.smallBurstSpeed,
            7 => s.lightningSpeed,
            8 => s.supermanSpeed,
            _ => 1
        });
        if (kind == 9)
            rate = 1;
        double duration = kind switch
        {
            1 => 3.8,
            3 or 4 => 3.4,
            8 => 2.2,
            9 => Math.Max(6, s.crashEventCount * 1.5 + 4),
            10 => .8,
            11 => 1.4,
            12 => .6,
            _ => 2.8
        };
        var e = new EffectState { Kind = kind, X = (float)Random.Between(.2, .8), Y = (float)Random.Between(.25, .75), Seed = Random.Int(65535), Started = Elapsed, Rate = rate, Duration = duration / rate, Child = child, SourceColumns = Columns, SourceRows = Rows, NextChild = Elapsed + .4 };
        if (kind >= 2 && kind <= 4 && !s.burstRandomPosition)
            e.X = e.Y = .5f;
        if ((kind == 3 || kind == 4) && !s.seamlessEffects)
            e.Frozen = (Cell[])Cells.Clone();
        if (kind == 7)
        {
            e.Paths = Sequences.Lightning(Columns, Rows, GlyphCount, ref Random);
            e.Duration = (e.Paths.Count + 1) * .1 / rate;
        }
        if (kind == 6)
        {
            var options = child ? s.Clone() : s;
            if (child)
                options.dropCount = 8;
            Sequences.MakeDrops(e, options, GlyphCount, ref Random);
            e.Duration = e.Drops!.Count * .4 / rate;
        }
        if (kind == 9)
        {
            e.Field = new(Columns, Rows, (uint)e.Seed);
            e.Total = (int)s.crashEventCount;
            e.ScheduleRandom = new((ulong)e.Seed ^ 0x6372617368);
        }
        if (kind == 10 || kind == 11)
        {
            var rng = new RainRandom((uint)e.Seed);
            bool varied = kind == 10 && rng.Int(3) == 2;
            e.Tiles = new float[16];
            for (int i = 0; i < 16; i++)
                e.Tiles[i] = !varied || rng.Int(6) == 0 ? 1 : rng.Int(5) * .25f;
            e.Duration = (kind == 10 ? .4 : .7) / s.speed;
        }
        if (kind == 12)
        {
            e.ScreenError = ScreenError.Make((uint)e.Seed);
            e.Duration = e.ScreenError.Duration / s.speed;
        }
        Effects.Add(e);
    }
    static List<int> ChildChoices(Settings s)
    {
        var c = new List<int>();
        if (s.crashDejaVu)
            c.Add(1);
        if (s.crashBursts)
        {
            if (s.movementBursts)
                c.Add(2);
            if (s.stillBursts)
                c.Add(3);
            if (s.traceBursts)
                c.Add(4);
        }
        if (s.crashSmallBursts)
            c.Add(5);
        if (s.crashDrops)
            c.Add(6);
        if (s.crashLightning)
            c.Add(7);
        if (s.crashSuperman)
            c.Add(8);
        return c;
    }
    static string ChildKey(int kind) => kind switch { 1 => "crashDejaVu", 2 or 3 or 4 => "crashBursts", 5 => "crashSmallBursts", 6 => "crashDrops", 7 => "crashLightning", 8 => "crashSuperman", 10 => "crashCodeFlashes", 11 => "crashBrightFlashes", 12 => "crashScreenErrors", _ => "displayFailure" };
    void AdvanceStorm(EffectState root, Settings s, double dt)
    {
        root.Field?.Advance(dt * s.speed);
        if (root.Finishing)
            return;
        root.Clock += dt * s.speed * 10000;
        bool HasMajor() => Effects.Any(e => e.Child && e.Kind < 9);
        bool HasFlash(int a, int b = 0) => Effects.Any(e => e.Child && (e.Kind == a || e.Kind == b));
        if (!HasMajor() && root.ChildCount < root.Total && root.Clock + 1e-7 >= root.NextMajor)
        {
            var choices = ChildChoices(s);
            if (choices.Count > 0)
            {
                Trigger(choices[root.ScheduleRandom.Int(choices.Count)], s, true);
                root.NextMajor = root.Clock;
            }
            else
                root.NextMajor = root.Clock + 3000;
            root.ChildCount++;
        }
        if (root.ChildCount == root.Total && !HasMajor() && root.Clock + 1e-7 >= root.NextMajor)
            root.Finishing = true;
        else
        {
            int tick = (int)((root.Clock + 1e-7) / (Math.Max(1, root.SourceRows / 6) * 1000));
            while (root.FlashTick < tick)
            {
                root.FlashTick++;
                if ((s.crashCodeFlashes || s.crashBrightFlashes) && !HasFlash(10, 11) && root.ScheduleRandom.Int(3) == 0)
                {
                    bool bright = s.crashBrightFlashes && (!s.crashCodeFlashes || root.ScheduleRandom.Int(3) == 1);
                    Trigger(bright ? 11 : 10, s, true);
                }
                if (s.crashScreenErrors && !HasFlash(12) && root.ScheduleRandom.Int(2) == 0)
                    Trigger(12, s, true);
            }
        }
        double end = Elapsed;
        foreach (var e in Effects)
            if (e.Child)
                end = Math.Max(end, e.Started + e.Duration);
        root.Duration = end + (root.Finishing ? 2 : 3) - root.Started;
    }

    public int FillEvents(GpuEvent[] gpu, uint[] drops, Vector4[] lightning, float[] mask, Settings s)
    {
        Array.Fill(drops, 65535u);
        Array.Clear(lightning);
        Array.Clear(mask);
        Effects.FirstOrDefault(e => e.Kind == 9)?.Field?.Fill(mask, Columns, Rows);
        foreach (var flash in Effects)
        {
            if (flash.Tiles == null)
                continue;
            float envelope = (float)Math.Min(1, (1 - (Elapsed - flash.Started) / flash.Duration) * (flash.Kind == 11 ? 7.0 / 6 : 4.0 / 3));
            for (int tile = 0; tile < 16; tile++)
            {
                int x0 = Columns * (tile % 4) / 4, x1 = Columns * (tile % 4 + 1) / 4, y0 = Rows * (tile / 4) / 4, y1 = Rows * (tile / 4 + 1) / 4;
                float light = flash.Tiles[tile] * envelope;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        mask[y * Columns + x] -= light;
            }
        }
        for (int i = 0; i < Effects.Count; i++)
        {
            var e = Effects[i];
            double age = (Elapsed - e.Started) * e.Rate, p = (Elapsed - e.Started) / e.Duration;
            var appearance = new Vector4(0, 3, (float)s.burstWidth, .22f);
            if (e.Kind == 1)
            {
                var (flags, count) = DejaStyle(s, (uint)e.Seed);
                appearance.X = flags;
                appearance.Y = count;
            }
            if (e.Kind == 5)
                appearance = new((s.smallBurstRandomSize ? 1 : 0) | (s.smallBurstFade ? 2 : 0) | (s.smallBurstSolid ? 4 : 0), (float)s.smallBurstCount, (float)s.smallBurstWidth, (float)s.smallBurstSize);
            if (e.Kind == 6 && e.Drops != null)
            {
                int slot = Math.Min(e.Drops.Count - 1, (int)(age / .4));
                appearance.X = e.DropColumns![slot] < 0 ? -1 : Math.Min(Columns - 1, e.DropColumns[slot] * Columns / e.SourceColumns);
                if (appearance.X >= 0)
                    for (int y = 0; y < Rows; y++)
                    {
                        var v = e.Drops[slot][Math.Min(e.SourceRows - 1, y * e.SourceRows / Rows)];
                        drops[i * Rows + y] = v == 65535 ? v : v % (uint)GlyphCount;
                    }
                age *= 10;
            }
            if (e.Kind == 7 && e.Paths != null)
            {
                double phase = age / .1;
                int index = (int)phase;
                float fraction = (float)(phase - index);
                AddPath(e, index - 1, 1 - fraction, lightning, s);
                AddPath(e, index, fraction, lightning, s);
            }
            float strength = (float)s.effectStrength;
            float centerX = e.X;
            if (e.Tiles != null)
                appearance = new(1, 0, 0, (float)Math.Min(1, (1 - p) * (e.Kind == 11 ? 7.0 / 6 : 4.0 / 3)));
            if (e.ScreenError != null)
            {
                var (bounds, light) = e.ScreenError.Geometry(p);
                appearance = bounds;
                strength *= light;
                centerX = e.ScreenError.Style;
            }
            gpu[i] = new()
            {
                Location = new(centerX, e.Y, (float)p, e.Kind),
                Parameters = new(e.Seed, strength, (float)age, (float)e.Duration),
                Appearance = appearance
            };
        }
        return Effects.Count;
    }
    void AddPath(EffectState e, int index, float weight, Vector4[] output, Settings s)
    {
        if (index < 0 || index >= e.Paths!.Count)
            return;
        var path = e.Paths[index];
        for (int j = 0; j < path.Length; j += 4)
        {
            if (path[j + 3] == 0)
                continue;
            int x = Math.Min(Columns - 1, path[j] * Columns / e.SourceColumns), y = Math.Min(Rows - 1, path[j + 1] * Rows / e.SourceRows), i = y * Columns + x;
            float intensity = weight * (float)s.effectStrength * (path[j + 3] == 1 ? .8f : 1.2f);
            if (intensity > output[i].Y)
            {
                output[i].Z = output[i].X;
                output[i].W = output[i].Y;
                output[i].X = path[j + 2] % GlyphCount;
                output[i].Y = intensity;
            }
            else if (intensity > output[i].W)
            {
                output[i].Z = path[j + 2] % GlyphCount;
                output[i].W = intensity;
            }
        }
    }
    public Cell[] DisplayCells(Cell[] scratch, Settings s)
    {
        if (s.seamlessEffects)
            return Cells;
        var e = Effects.LastOrDefault(e => e.Frozen?.Length == Cells.Length);
        if (e == null)
            return Cells;
        Array.Copy(Cells, scratch, Cells.Length);
        double age = (Elapsed - e.Started) * e.Rate, radius = Math.Max(0, age - .9) * .55;
        float dim = (float)(1 - Math.Min(1, age / .18) * .9);
        for (int i = 0; i < Cells.Length; i++)
        {
            double x = (i % Columns + .5) / Columns, y = (i / Columns + .5) / Rows;
            if (Math.Max(Math.Abs(x - e.X), Math.Abs(y - e.Y) * .78) > radius)
            {
                scratch[i] = e.Frozen![i];
                if (e.Kind == 4 && Cells[i].Light.X > .5)
                    scratch[i] = Cells[i];
                else
                    scratch[i].Ink.W *= dim;
            }
        }
        return scratch;
    }
    public void SaveCheckpoint(string path)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(this, Settings.Json);
        if (bytes.Length > 16000000)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path + ".tmp", bytes);
        File.Move(path + ".tmp", path, true);
    }
    public static Simulation? LoadCheckpoint(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 16000000)
                return null;
            var s = JsonSerializer.Deserialize<Simulation>(File.ReadAllBytes(path), Settings.Json);
            if (s == null || s.Columns < 1 || s.Rows < 1 || s.Columns > 1280 || s.Rows > 720 || s.Columns * s.Rows > 200000 || s.Cells.Length != s.Columns * s.Rows || s.Streams.Length != s.Columns || s.Effects.Count > 12 || s.Deadlines.Length != 10 || !double.IsFinite(s.Elapsed) || s.Elapsed < 0 || s.Elapsed > 1e12)
                return null;
            if (s.Cells.Any(c => !float.IsFinite(c.Ink.W) || !float.IsFinite(c.Ink.X) || c.Ink.X < 0 || c.Ink.X > 1024) || s.Streams.Any(st => !double.IsFinite(st.Head) || Math.Abs(st.Head) > 1e6 || !double.IsFinite(st.Speed) || st.Speed < 0 || st.Speed > 1000))
                return null;
            static bool Finite(Vector4 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W);
            if (s.GlyphCount < 1 || s.GlyphCount > 1024 || s.Cells.Any(c => !Finite(c.Ink) || !Finite(c.Light)) ||
                s.Deadlines.Any(d => !double.IsFinite(d) || d < 0 || d > 1e12) ||
                !double.IsFinite(s.Accumulator) || Math.Abs(s.Accumulator) > 10 ||
                !double.IsFinite(s.BootProgress) || s.BootProgress < 0 || s.BootProgress > 114 ||
                !double.IsFinite(s.TitleStarted) || !double.IsFinite(s.NextRotation) || !double.IsFinite(s.NextEffect))
                return null;
            foreach (var e in s.Effects)
            {
                if (e.Kind < 1 || e.Kind > 12 || e.SourceColumns < 1 || e.SourceColumns > 1280 || e.SourceRows < 1 || e.SourceRows > 720 ||
                    !double.IsFinite(e.Started) || !double.IsFinite(e.Duration) || e.Duration <= 0 || e.Duration > 8192 ||
                    !double.IsFinite(e.Rate) || e.Rate <= 0 || e.Rate > 2048 || !float.IsFinite(e.Seed) || e.Seed < 0 || e.Seed > 65535 ||
                    !double.IsFinite(e.Clock) || e.Clock < 0 || e.Clock > 1e9 || e.Total < 0 || e.Total > 30 || e.ChildCount < 0 || e.ChildCount > 30)
                    return null;
                if (e.Frozen != null && (e.Frozen.Length != s.Cells.Length || e.Frozen.Any(c => !Finite(c.Ink) || !Finite(c.Light)))) return null;
                if (e.Paths != null)
                {
                    if (e.Paths.Count < 16 || e.Paths.Count > 47) return null;
                    foreach (var pathData in e.Paths)
                    {
                        if (pathData.Length % 4 != 0 || pathData.Length > 8192 * 4) return null;
                        for (int i = 0; i < pathData.Length; i += 4)
                            if (pathData[i] < 0 || pathData[i] >= e.SourceColumns || pathData[i + 1] < 0 || pathData[i + 1] >= e.SourceRows || pathData[i + 2] < 0 || pathData[i + 2] > 1024 || pathData[i + 3] < 0 || pathData[i + 3] > 2) return null;
                    }
                }
                if (e.Drops != null)
                {
                    if (e.Drops.Count < 1 || e.Drops.Count > 512 || e.DropColumns?.Length != e.Drops.Count) return null;
                    for (int i = 0; i < e.Drops.Count; i++)
                        if (e.DropColumns[i] < -1 || e.DropColumns[i] >= e.SourceColumns || e.Drops[i].Length != (e.DropColumns[i] < 0 ? 0 : e.SourceRows) || e.Drops[i].Any(g => g != 65535 && g >= 1024)) return null;
                }
                if (e.Tiles != null && (e.Tiles.Length != 16 || e.Tiles.Any(x => !float.IsFinite(x) || x < 0 || x > 1))) return null;
                if (e.Field != null && (e.Field.Columns < 1 || e.Field.Rows < 1 || e.Field.Rectangles.Count > 512 || e.Field.Rectangles.Any(r => !Finite(r.Bounds)))) return null;
                if (e.ScreenError != null && (!Finite(new Vector4(e.ScreenError.Horizontal, e.ScreenError.FinalY, e.ScreenError.TravelY)) || !float.IsFinite(e.ScreenError.HalfHeight))) return null;
            }
            if (s.Title != null && (s.Title.Text == null || s.Title.Text.Length > 64 || s.Title.Columns < 1 || s.Title.Columns > 1280 || s.Title.Rows < 1 || s.Title.Rows > 10000 || s.Title.Slots.Count > 64 ||
                !double.IsFinite(s.Title.Travel) || !double.IsFinite(s.Title.Elapsed) || !double.IsFinite(s.Title.Settled) || !double.IsFinite(s.Title.ZoomProgress) ||
                s.Title.Slots.Any(t => t.Index < 0 || t.Index >= 192 || !double.IsFinite(t.Start) || !double.IsFinite(t.Target) || !double.IsFinite(t.Length) || !double.IsFinite(t.Departure) || !double.IsFinite(t.Fade)))) return null;
            return s;
        }
        catch { return null; }
    }
}
