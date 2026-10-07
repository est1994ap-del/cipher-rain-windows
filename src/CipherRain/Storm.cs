using System;
using System.Numerics;
using System.Collections.Generic;
namespace CipherRain;
public sealed class StormRectangle
{
    public Vector4 Bounds; public bool PositiveX, PositiveY;
}
public sealed class StormField
{
    public int Columns, Rows; public List<StormRectangle> Rectangles = new(); public RainRandom Random;
    public StormField()
    {
    }
    public StormField(int columns, int rows, uint seed)
    {
        Columns = columns;
        Rows = rows;
        Random = new(seed);
        for (int i = 0; i < Math.Min(512, columns * rows / 50); i++)
        {
            int w = Random.Int(columns / 10 + 10), h = Random.Int(rows / 10 + 10), x = Random.Int(columns + w) - w, y = Random.Int(rows + h) - h;
            Rectangles.Add(new()
            {
                Bounds = new(x, y, x + w + 4, y + h + 2),
                PositiveX = Random.Int(2) != 0,
                PositiveY = Random.Int(2) != 0
            });
        }
    }
    public void Advance(double dt)
    {
        float delta = (float)Math.Min(.25, dt);
        foreach (var r in Rectangles)
        {
            if (r.Bounds.Z < 0)
            {
                r.PositiveX = true;
                if (Random.Int(4) == 0)
                    r.PositiveY = !r.PositiveY;
            }
            if (r.Bounds.X >= Columns)
            {
                r.PositiveX = false;
                if (Random.Int(4) == 0)
                    r.PositiveY = !r.PositiveY;
            }
            if (r.Bounds.W < 0)
            {
                r.PositiveY = true;
                if (Random.Int(4) == 0)
                    r.PositiveX = !r.PositiveX;
            }
            if (r.Bounds.Y >= Rows)
            {
                r.PositiveY = false;
                if (Random.Int(4) == 0)
                    r.PositiveX = !r.PositiveX;
            }
            float dx = r.PositiveX ? delta : -delta, dy = r.PositiveY ? delta : -delta;
            r.Bounds += new Vector4(dx, dy, dx, dy);
        }
    }
    public void Fill(float[] output, int columns, int rows)
    {
        Array.Clear(output);
        float sx = (float)columns / Columns, sy = (float)rows / Rows;
        foreach (var r in Rectangles)
        {
            var b = r.Bounds;
            float x0 = Math.Max(0, b.X * sx), x1 = Math.Min(columns, b.Z * sx), y0 = Math.Max(0, b.Y * sy), y1 = Math.Min(rows, b.W * sy);
            if (x0 >= x1 || y0 >= y1)
                continue;
            int left = (int)x0, right = Math.Min(columns - 1, (int)Math.Ceiling(x1) - 1), top = (int)y0, bottom = Math.Min(rows - 1, (int)Math.Ceiling(y1) - 1);
            float la = Math.Min(x1, left + 1) - x0, ra = x1 - Math.Max(x0, right);
            for (int y = top; y <= bottom; y++)
            {
                float amount = Math.Min(y1, y + 1) - Math.Max(y0, y);
                int row = y * columns;
                void Add(int start, int end, float v)
                {
                    if (start >= end)
                        return;
                    output[row + start] += v;
                    if (end < columns)
                        output[row + end] -= v;
                }
                Add(left, left + 1, la * amount);
                if (right > left)
                {
                    Add(right, right + 1, ra * amount);
                    Add(left + 1, right, amount);
                }
            }
        }
        for (int y = 0; y < rows; y++)
        {
            float coverage = 0;
            for (int x = 0; x < columns; x++)
            {
                int i = y * columns + x;
                coverage += output[i];
                output[i] = Math.Clamp(coverage, 0, .96f);
            }
        }
    }
}
public sealed class ScreenError
{
    public int Style, DurationMs = 6000; public Vector2 Horizontal = new(0, 1); public float FinalY = .5f, TravelY, HalfHeight;
    public double Duration => DurationMs * (.65 / 6000);
    public static ScreenError Make(uint seed)
    {
        var random = new RainRandom(seed);
        var e = new ScreenError { Style = random.Int(3) };
        if (e.Style == 1)
        {
            if (random.Int(2) != 0)
                e.Horizontal.Y = (float)(1.0 / 3 + random.Unit() * 2 / 3);
            else
                e.Horizontal.X = (float)(2.0 / 3 - random.Unit() * 2 / 3);
            e.FinalY = (float)random.Unit();
            e.DurationMs = 1000 + random.Int(2000);
        }
        else if (e.Style == 2)
        {
            int span = random.Int(6);
            e.FinalY = (float)(1.5 - random.Unit() * 2);
            e.HalfHeight = (float)(.1 + random.Unit() * .25);
            e.Horizontal = new(-.125f, 1.125f);
            if (span == 4)
                e.Horizontal.Y = (float)(.5 + random.Unit() * .5);
            if (span == 5)
                e.Horizontal.X = (float)(random.Unit() * .5);
            e.TravelY = (float)random.Unit() * (random.Int(2) == 0 ? -1 : 1);
        }
        return e;
    }
    public (Vector4, float) Geometry(double p)
    {
        float remaining = (float)(1 - p);
        return (new(Horizontal.X, Horizontal.Y, FinalY + TravelY * remaining, HalfHeight), Math.Max(0, Style == 2 ? 2 * (.5f - Math.Abs(remaining - .5f)) : remaining));
    }
}
