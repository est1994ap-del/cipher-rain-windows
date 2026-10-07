using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
namespace CipherRain;
public static class GlyphAtlas
{
    const int Size = 128, Mips = 8;
    public static ID3D11ShaderResourceView Create(ID3D11Device device, Settings s, bool title, out int count)
    {
        string kana = "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜ012345789:｡";
        bool terminal = s.glyphSet == "Terminal" || s.glyphSet == "Operator" || s.glyphSet == "Legacy";
        string chars = title ? TitleSequence.Padded(s.titleText) : s.glyphSet == "Operator" ? "001101001101001101<>/{}[]+*=#$%&" : terminal ? "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ<>/{}[]:;=+" : kana;
        if (chars.Length == 0)
            chars = " ";
        var alphas = new List<byte[]>();
        if (!title && s.usePrivateGlyphs)
        {
            int start = s.glyphSet switch
            {
                "Narrow" => 100,
                "Terminal" => 200,
                "Operator" => 300,
                "Dense" => 400,
                "Legacy" => 700,
                _ => 0
            };
            for (int i = 0; i < (start >= 400 ? 100 : 55); i++)
            {
                var p = Path.Combine(Settings.DataDir, "Private Glyphs", (start + i).ToString());
                if (File.Exists(p))
                    try
                    {
                        alphas.Add(ReadPrivate(p));
                    }
                    catch { }
            }
        }
        if (alphas.Count < 40 && !title)
            alphas.Clear();
        if (alphas.Count == 0)
            foreach (char ch in chars)
                alphas.Add(Raster(ch, title ? "Courier New" : terminal ? "Consolas" : "Yu Gothic", !title && (s.glyphSet == "Operator" || s.glyphSet == "Dense" || s.glyphSet == "Legacy"), !title && !terminal, !title && s.glyphSet == "Narrow" ? .67f : 1, !title));
        count = alphas.Count;
        var pins = new List<GCHandle>();
        var data = new SubresourceData[count * Mips];
        try
        {
            for (int slice = 0; slice < count; slice++)
            {
                var a = alphas[slice];
                var near = Blur(a, Size, 7);
                var far = Blur(a, Size, 21);
                var packed = new byte[Size * Size * 4];
                for (int i = 0; i < a.Length; i++)
                {
                    packed[i * 4] = a[i];
                    packed[i * 4 + 1] = near[i];
                    packed[i * 4 + 2] = far[i];
                    packed[i * 4 + 3] = 255;
                }
                int size = Size;
                for (int mip = 0; mip < Mips; mip++)
                {
                    var pin = GCHandle.Alloc(packed, GCHandleType.Pinned);
                    pins.Add(pin);
                    data[slice * Mips + mip] = new(pin.AddrOfPinnedObject(), (uint)(size * 4), (uint)packed.Length);
                    if (size > 1)
                    {
                        var small = new byte[size * size];
                        for (int y = 0; y < size / 2; y++)
                            for (int x = 0; x < size / 2; x++)
                                for (int c = 0; c < 4; c++)
                                    small[(y * size / 2 + x) * 4 + c] = (byte)((packed[(y * 2 * size + x * 2) * 4 + c] + packed[(y * 2 * size + x * 2 + 1) * 4 + c] + packed[((y * 2 + 1) * size + x * 2) * 4 + c] + packed[((y * 2 + 1) * size + x * 2 + 1) * 4 + c]) / 4);
                        packed = small;
                        size /= 2;
                    }
                }
            }
            using var tex = device.CreateTexture2D(new Texture2DDescription { Width = Size, Height = Size, MipLevels = Mips, ArraySize = (uint)count, Format = Format.R8G8B8A8_UNorm, SampleDescription = new(1, 0), Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource }, data);
            return device.CreateShaderResourceView(tex);
        }
        finally { foreach (var pin in pins) pin.Free(); }
    }
    static byte[] ReadPrivate(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 33 || System.Text.Encoding.ASCII.GetString(bytes, 0, 7) != "aBitmap")
            throw new InvalidDataException();
        int w = BitConverter.ToInt32(bytes, 8), h = BitConverter.ToInt32(bytes, 12);
        if (w < 1 || h < 1 || w > 1024 || h > 1024)
            throw new InvalidDataException();
        using var z = new ZLibStream(new MemoryStream(bytes, 32, bytes.Length - 32), CompressionMode.Decompress);
        var raw = new byte[w * h * 4];
        z.ReadExactly(raw);
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int a = raw[(y * w + x) * 4 + 3];
                bmp.SetPixel(x, y, Color.FromArgb(255, a, a, a));
            }
        using var resized = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(bmp, 0, 0, Size, Size);
        }
        return Coverage(resized, false);
    }
    static byte[] Raster(char c, string font, bool bold, bool mirror, float scale, bool flip)
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var f = new Font(font, Size * .74f, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            using var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            if (mirror)
            {
                g.TranslateTransform(Size, 0);
                g.ScaleTransform(-1, 1);
            }
            g.TranslateTransform(Size * (1 - scale) / 2, 0);
            g.ScaleTransform(scale, 1);
            g.DrawString(c.ToString(), f, Brushes.White, new RectangleF(0, 0, Size, Size), sf);
        }
        return Coverage(bmp, flip);
    }
    static byte[] Coverage(Bitmap bmp, bool flip)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, Size, Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var raw = new byte[Size * Size * 4];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);
            var a = new byte[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    a[y * Size + x] = raw[((flip ? Size - 1 - y : y) * Size + x) * 4];
            return a;
        }
        finally { bmp.UnlockBits(data); }
    }
    static byte[] Blur(byte[] source, int n, int kernel)
    {
        var a = (byte[])source.Clone();
        int radius = kernel / 2;
        for (int pass = 0; pass < 3; pass++)
        {
            var temp = new int[n * n];
            var b = new byte[n * n];
            for (int y = 0; y < n; y++)
            {
                int sum = 0;
                for (int x = -radius; x < n + radius; x++)
                {
                    int add = x + radius, remove = x - radius - 1;
                    if (add >= 0 && add < n)
                        sum += a[y * n + add];
                    if (remove >= 0 && remove < n)
                        sum -= a[y * n + remove];
                    if (x >= 0 && x < n)
                        temp[y * n + x] = sum;
                }
            }
            for (int x = 0; x < n; x++)
            {
                int sum = 0;
                for (int y = -radius; y < n + radius; y++)
                {
                    int add = y + radius, remove = y - radius - 1;
                    if (add >= 0 && add < n)
                        sum += temp[add * n + x];
                    if (remove >= 0 && remove < n)
                        sum -= temp[remove * n + x];
                    if (y >= 0 && y < n)
                        b[y * n + x] = (byte)(sum / (kernel * kernel));
                }
            }
            a = b;
        }
        return a;
    }
}
