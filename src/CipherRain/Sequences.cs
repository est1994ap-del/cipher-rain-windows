using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
namespace CipherRain;
public sealed class TitleSlot
{
    public int Index; public double Start, Target, Length, Departure, Fade;
}
public sealed class TitleSequence
{
    public string Text = ""; public int Columns, Rows; public double Elapsed, Travel, ZoomProgress, Settled; public List<TitleSlot> Slots = new();
    public TitleSequence()
    {
    }
    public TitleSequence(string text, int columns, int rows, uint seed)
    {
        Text = text;
        var lines = text.Split('\n');
        int width = lines.Max(l => l.Length);
        Columns = Math.Max(columns, width * 2 + 2);
        Rows = Math.Max(8, (int)Math.Round((double)rows * Columns / Math.Max(1, columns)));
        var random = new RainRandom(seed | 1UL << 32);
        var previous = new Dictionary<int, double>();
        for (int line = 0; line < lines.Length; line++)
        {
            var value = lines[line];
            int padding = (width - value.Length) / 2;
            double target = Rows / 2 + line * 2 - (lines.Length - 1);
            for (int j = 0; j < value.Length; j++)
            {
                if (char.IsWhiteSpace(value[j]))
                    continue;
                int col = Columns / 2 - value.Length + j * 2;
                double start = -(1 + random.Int(Rows * 2));
                if (previous.TryGetValue(col, out var prev))
                    while (start >= prev)
                        start -= 1 + random.Int(Rows * 2);
                previous[col] = start;
                Slots.Add(new()
                {
                    Index = line * width + padding + j,
                    Start = start,
                    Target = target,
                    Length = 12 + random.Int(Math.Max(1, Rows / 2))
                });
            }
        }
        var order = Enumerable.Range(0, Slots.Count).ToArray();
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = random.Int(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        for (int rank = 0; rank < order.Length; rank++)
            Slots[order[rank]].Departure = Math.Min(18, rank * 20 / Math.Max(1, order.Length)) * .125;
    }
    public double Arrival => Slots.Count == 0 ? 1 : Slots.Max(s => s.Target - s.Start);
    public double Zoom => 1 / (1 - .45 * (1 - Math.Cos(Math.Min(1, ZoomProgress) * Math.PI)));
    public void Advance(double dt, Settings s)
    {
        double rate = 20 * s.speed * (s.titleDoubleSpeed ? 2 : 1), old = Travel;
        Travel += dt * rate;
        Elapsed += dt;
        ZoomProgress = Math.Min(1, ZoomProgress + dt * Math.Min(12, 20 * s.speed) * .5 / Math.Max(1, Slots.Count == 0 ? 1 : Slots.Max(x => -x.Start)));
        double prev = Settled;
        Settled += Math.Max(0, dt - Math.Max(0, Arrival - old) / Math.Max(.001, rate));
        foreach (var slot in Slots)
        {
            double fadeDt = Math.Max(0, Settled - Math.Max(prev, s.titleHold + slot.Departure));
            slot.Fade = Math.Min(.5, slot.Fade + fadeDt * (s.titleDoubleSpeed ? 2 : 1));
        }
    }
    public bool Finished(Settings s) => Slots.All(x => x.Fade >= .5) && Settled >= s.titleHold + 2.75 + 3.6;
    public void Fill(Vector4[] values, bool hide)
    {
        Array.Clear(values);
        double stepped = Math.Floor(Travel + 1e-7);
        foreach (var s in Slots)
        {
            if (s.Index >= values.Length)
                continue;
            double head = Math.Min(s.Target, s.Start + stepped), tail = Math.Min(head, s.Start - s.Length + stepped), h = hide ? Math.Max(0, 1 - Math.Max(0, Travel - (s.Target - s.Start)) / 2.5) : 1;
            values[s.Index] = new((float)head, (float)tail, (float)Math.Max(0, 1 - s.Fade * 2), (float)h);
        }
    }
    public static string Padded(string text)
    {
        var lines = text.Split('\n');
        int width = Math.Max(1, lines.Max(l => l.Length));
        return string.Concat(lines.Select(l => new string(' ', (width - l.Length) / 2) + l + new string(' ', width - l.Length - (width - l.Length) / 2)));
    }
}
public static class Sequences
{
    public static List<int[]> Lightning(int columns, int rows, int glyphs, ref RainRandom random)
    {
        int count = 16 + random.Int(32), direction = random.Int(2) == 0 ? 1 : -1, limit = Math.Min(8192, (columns + rows) * 8);
        var paths = new List<int[]>();
        for (int n = 0; n < count; n++)
        {
            var points = new SortedDictionary<int, int[]>();
            var walks = new Stack<(int, int, bool, int)>();
            walks.Push((direction > 0 ? 0 : columns - 1, 0, columns > rows, 0));
            int attempts = 0;
            while (walks.TryPop(out var walk))
            {
                var (x, y, horizontal, depth) = walk;
                while (x >= 0 && x < columns && y < rows && attempts++ < limit)
                {
                    int ratio = horizontal ? columns / rows : rows / columns, step = random.Int(ratio + 3), nx = x + ((horizontal ? step != 1 : step < 2) ? direction : 0), ny = y + ((horizontal ? step < 2 : step != 1) ? 1 : 0);
                    if (nx < 0 || nx >= columns || ny >= rows)
                        break;
                    if (depth < 4 && random.Int(Math.Max(2, columns * 2)) == 0)
                        walks.Push((nx, ny, !horizontal, depth + 1));
                    int brightness = random.Int(2) == 0 ? 0 : 1 + random.Int(2), key = ny * columns + nx;
                    if (!points.ContainsKey(key))
                        points[key] = new[] { nx, ny, random.Int(glyphs), brightness };
                    if (random.Int(2) == 0)
                    {
                        x = nx;
                        y = ny;
                    }
                }
            }
            paths.Add(points.Values.SelectMany(x => x).ToArray());
        }
        int[] Clip(int[] p, Func<int, bool> predicate)
        {
            var r = new List<int>();
            for (int i = 0; i < p.Length; i += 4)
                if (predicate(p[i + 1]))
                    r.AddRange(p.AsSpan(i, 4).ToArray());
            return r.ToArray();
        }
        paths[0] = Clip(paths[2], y => y <= rows / 3);
        paths[1] = Clip(paths[2], y => y <= rows * 2 / 3);
        paths[count - 2] = Clip(paths[count - 3], y => y >= rows / 3);
        paths[count - 1] = Clip(paths[count - 3], y => y >= rows * 2 / 3);
        return paths;
    }
    public static void MakeDrops(EffectState e, Settings s, int glyphs, ref RainRandom random)
    {
        int average = (int)s.dropCount, target = Math.Max(1, average / 2 + random.Int(average)), emitted = 0;
        bool positive = true;
        var cols = new List<int>();
        e.Drops = new();
        while (emitted < target && e.Drops.Count < 512)
        {
            if (random.Int(6) == 0)
            {
                cols.Add(-1);
                e.Drops.Add(Array.Empty<uint>());
                continue;
            }
            cols.Add(random.Int(e.SourceColumns));
            var pattern = new uint[e.SourceRows];
            for (int y = 0; y < pattern.Length; y++)
            {
                double density = positive ? s.dropPositiveDensity : s.dropNegativeDensity;
                if (random.Int(Math.Max(1, e.SourceRows / 4)) == 0)
                    positive = !positive;
                pattern[y] = random.Unit() < density ? (uint)random.Int(glyphs) : 65535u;
            }
            e.Drops.Add(pattern);
            emitted++;
        }
        e.DropColumns = cols.ToArray();
    }
    public static int Boot(float t, Vector2 cell, uint seed, BootQuad[] quads)
    {
        int count = 0;
        if (t < 0 || t >= 114)
            return 0;
        void Add(Vector4 bounds, float gain, Vector4? uv = null, bool broad = true)
        {
            if (gain <= 0 || bounds.Z <= bounds.X || bounds.W <= bounds.Y || count >= quads.Length)
                return;
            quads[count++] = new()
            {
                Bounds = bounds,
                UV = uv ?? new Vector4(0, 0, 1, 1),
                Light = new(gain, broad ? 1 : 0, 0, 0)
            };
        }
        if (t > 30 && t < 50)
        {
            float gain = t <= 36 ? (t - 30) / 18 : t <= 46 ? .5f + (t - 36) * .05f : 1 + (46 - t) * .25f;
            Add(new(.5f - cell.X, .5f - cell.Y, .5f + cell.X, .5f + cell.Y), gain);
        }
        if (t > 46 && t < 52)
        {
            float half = (t - 46) * .25f;
            Add(new(.5f - cell.X * 1.1f, .5f - half, .5f + cell.X * 1.1f, .5f + half), t <= 50 ? 1.2f : 1.2f + (50 - t) * .6f);
        }
        if (t > 50 && t <= 58)
        {
            float half = (t - 50) / 6;
            Add(new(.5f - half, -.7f, .5f + half, 1.7f), Math.Min(1, (t - 50) * .5f));
        }
        foreach (float center in new[] { 74f, 94f })
            if (t > center - 4 && t < center + 4)
            {
                float low = t <= center - 2 ? .5f : .5f + (center - 2 - t) / 12;
                Add(new(0, 0, 1, 1), Math.Max(0, 1 - Math.Abs(t - center) * .5f), new(0, low, 1, 1));
            }
        if (t > 58)
            Add(new(0, 0, 1, 1), .5f * (t <= 108 ? 1 : (114 - t) / 6), new(.2f, .2f, .8f, .8f));
        if (t > 52 && t < 108)
        {
            uint slot = (uint)(int)(t - 52) / 2;
            uint Draw(uint salt, uint n) => Simulation.Hash(unchecked(seed + slot * 0x9e3779b9 + salt * 0x85ebca6b)) % n;
            if (Draw(1, 3) != 0)
            {
                float width = (Draw(2, 60) + 20) * .01f, drift = Draw(3, 2) == 0 ? 0 : (10 - (float)Draw(4, 21)) * .01f, center = Draw(5, 101) * .01f + drift * (t - 52 - slot * 2) * 3;
                bool vertical = Draw(6, 2) == 0, split = Draw(7, 8) == 0;
                void Band(float low, float high)
                {
                    if (vertical)
                        Add(new(low, 0, high, 1), .5f, new(0, .2f, 1, .8f), false);
                    else
                        Add(new(0, low, 1, high), .5f, new(.2f, 0, .8f, 1), false);
                }
                if (split)
                {
                    Band(center - width * .5f, center - width * .2f);
                    Band(center + width * .2f, center + width * .5f);
                }
                else
                    Band(center - width, center + width);
            }
        }
        return count;
    }
}
