using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;
namespace CipherRain;
[StructLayout(LayoutKind.Sequential)]
public struct Uniforms
{
    public Vector4 Viewport, Grid, Style, TailColor, HeadColor, Title, TitleOptions, TitleLayout, Boot, TitleMotion, TitleLines;
}
public sealed class Renderer : IDisposable
{
    ID3D11Device device; ID3D11DeviceContext context; IDXGISwapChain2 swap; ID3D11RenderTargetView target; ID3D11VertexShader vertex; ID3D11PixelShader pixel; ID3D11SamplerState sampler; ID3D11RasterizerState raster;
    ID3D11Buffer constants; ID3D11Buffer[] buffers = new ID3D11Buffer[7]; ID3D11ShaderResourceView[] views = new ID3D11ShaderResourceView[9];
    GpuEvent[] events = new GpuEvent[12]; BootQuad[] boot = new BootQuad[8]; Vector4[] title = new Vector4[192], lightning = Array.Empty<Vector4>(); uint[] drops = Array.Empty<uint>(); float[] mask = Array.Empty<float>(); Cell[] scratch = Array.Empty<Cell>();
    public int Width, Height, GlyphCount; public IntPtr FrameReady; string glyphKey = "", titleKey = ""; public string AdapterName = "Direct3D 11"; int gridSize;
    public Renderer(IntPtr hwnd, int width, int height)
    {
        string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Shaders", "Rain.hlsl"));
        var vs = Compiler.Compile(source, "rainVertex", "Rain.hlsl", "vs_5_0");
        var ps = Compiler.Compile(source, "rainFragment", "Rain.hlsl", "ps_5_0");
        IDXGIAdapter1? chosen = null;
        Native.GetWindowRect(hwnd, out var windowRect);
        int cx = (windowRect.Left + windowRect.Right) / 2, cy = (windowRect.Top + windowRect.Bottom) / 2;
        using (var select = CreateDXGIFactory1<IDXGIFactory1>())
        {
            for (uint ai = 0; select.EnumAdapters1(ai, out var candidate).Success; ai++)
            {
                bool match = false;
                for (uint oi = 0; candidate.EnumOutputs(oi, out var output).Success; oi++)
                {
                    using (output)
                    {
                        var bounds = output.Description.DesktopCoordinates;
                        if (cx >= bounds.Left && cx < bounds.Right && cy >= bounds.Top && cy < bounds.Bottom)
                            match = true;
                    }
                }
                if (match)
                {
                    chosen = candidate;
                    break;
                }
                candidate.Dispose();
            }
        }
        try
        {
            D3D11CreateDevice(chosen, chosen == null ? DriverType.Hardware : DriverType.Unknown, DeviceCreationFlags.BgraSupport, new[] { FeatureLevel.Level_11_0 }, out device, out context).CheckError();
        }
        finally { chosen?.Dispose(); }
        using var dxgi = device.QueryInterface<IDXGIDevice>();
        using var adapter = dxgi.GetAdapter();
        AdapterName = adapter.Description.Description;
        using var factory = adapter.GetParent<IDXGIFactory2>();
        using var swap1 = factory.CreateSwapChainForHwnd(device, hwnd, new SwapChainDescription1 { Width = (uint)width, Height = (uint)height, Format = Format.B8G8R8A8_UNorm, BufferCount = 2, BufferUsage = Usage.RenderTargetOutput, SampleDescription = new(1, 0), SwapEffect = SwapEffect.FlipDiscard, Scaling = Scaling.Stretch, AlphaMode = AlphaMode.Ignore, Flags = SwapChainFlags.FrameLatencyWaitableObject });
        swap = swap1.QueryInterface<IDXGISwapChain2>();
        swap.MaximumFrameLatency = 1;
        FrameReady = swap.FrameLatencyWaitableObject;
        factory.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAltEnter);
        Width = width;
        Height = height;
        using (var back = swap.GetBuffer<ID3D11Texture2D>(0))
            target = device.CreateRenderTargetView(back);

        vertex = device.CreateVertexShader(vs.Span);
        pixel = device.CreatePixelShader(ps.Span);
        sampler = device.CreateSamplerState(new SamplerDescription { Filter = Filter.MinMagMipLinear, AddressU = TextureAddressMode.Clamp, AddressV = TextureAddressMode.Clamp, AddressW = TextureAddressMode.Clamp, MaxLOD = float.MaxValue });
        raster = device.CreateRasterizerState(new RasterizerDescription { CullMode = CullMode.None, FillMode = FillMode.Solid, DepthClipEnable = true });
        constants = device.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Uniforms>(), BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write));
    }
    void Buffer(int slot, int count, int stride)
    {
        views[slot]?.Dispose();
        buffers[slot]?.Dispose();
        buffers[slot] = device.CreateBuffer(new BufferDescription { ByteWidth = (uint)(Math.Max(1, count) * stride), BindFlags = BindFlags.ShaderResource, Usage = ResourceUsage.Dynamic, CPUAccessFlags = CpuAccessFlags.Write, MiscFlags = ResourceOptionFlags.BufferStructured, StructureByteStride = (uint)stride });
        views[slot] = device.CreateShaderResourceView(buffers[slot]);
    }
    unsafe void Upload<T>(ID3D11Buffer buffer, T[] values, int count) where T : unmanaged
    {
        var map = context.Map(buffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            fixed (T* ptr = values)
                System.Buffer.MemoryCopy(ptr, (void*)map.DataPointer, (long)count * sizeof(T), (long)count * sizeof(T));
        }
        finally { context.Unmap(buffer, 0); }
    }
    unsafe void UploadUniform(Uniforms u)
    {
        var map = context.Map(constants, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        *(Uniforms*)map.DataPointer = u;
        context.Unmap(constants, 0);
    }
    public void Prepare(Settings s, Simulation simulation, float scale)
    {
        string key = s.glyphSet + "/" + s.usePrivateGlyphs;
        if (key != glyphKey)
        {
            views[7]?.Dispose();
            views[7] = GlyphAtlas.Create(device, s, false, out GlyphCount);
            glyphKey = key;
        }
        if (titleKey != s.titleText || views[8] == null)
        {
            views[8]?.Dispose();
            views[8] = GlyphAtlas.Create(device, s, true, out _);
            titleKey = s.titleText;
        }
        float cell = (float)s.glyphSize * scale;
        simulation.Resize((int)Math.Ceiling(Width / cell), (int)Math.Ceiling(Height / cell) + 2, GlyphCount, s);
        if (gridSize != simulation.Cells.Length)
        {
            gridSize = simulation.Cells.Length;
            scratch = new Cell[gridSize];
            lightning = new Vector4[gridSize];
            mask = new float[gridSize];
            drops = new uint[12 * simulation.Rows];
            Buffer(0, gridSize, 32);
            Buffer(1, 12, 48);
            Buffer(2, drops.Length, 4);
            Buffer(3, gridSize, 16);
            Buffer(4, gridSize, 4);
            Buffer(5, 8, 48);
            Buffer(6, 192, 16);
        }
    }
    public void Resize(int w, int h)
    {
        if (w < 1 || h < 1 || w == Width && h == Height)
            return;
        context.OMSetRenderTargets(Array.Empty<ID3D11RenderTargetView>());
        target.Dispose();
        swap.ResizeBuffers(2, (uint)w, (uint)h, Format.B8G8R8A8_UNorm, SwapChainFlags.FrameLatencyWaitableObject).CheckError();
        using var back = swap.GetBuffer<ID3D11Texture2D>(0);
        target = device.CreateRenderTargetView(back);
        Width = w;
        Height = h;
    }
    public void Draw(Simulation sim, Settings s, float scale)
    {
        Prepare(s, sim, scale);
        int count = sim.FillEvents(events, drops, lightning, mask, s);
        float cell = (float)s.glyphSize * scale, phase = (s.bootEffect || sim.PreviewBoot) && sim.BootProgress < 114 ? (float)sim.BootProgress : -1;
        int bootCount = Sequences.Boot(phase, new(cell / Width, cell / Height), sim.BootSeed, boot);
        Array.Clear(title);
        var t = sim.Title?.Text == s.titleText ? sim.Title : null;
        t?.Fill(title, s.titleHideTrails);
        var lines = s.titleText.Split('\n');
        int titleColumns = 1;
        foreach (var l in lines)
            titleColumns = Math.Max(titleColumns, l.Length);
        var tail = s.palette switch
        {
            "Acid Green" => new Vector4(.28f, 1, .03f, 1),
            "Ice Blue" => new(.02f, .70f, .94f, 1),
            "Amber" => new(1, .55f, .06f, 1),
            _ => new(.01f, .72f, .20f, 1)
        };
        var head = s.palette switch
        {
            "Acid Green" => new Vector4(.92f, 1, .66f, 1),
            "Ice Blue" => new(.76f, .96f, 1, 1),
            "Amber" => new(1, .9f, .65f, 1),
            _ => new(.72f, 1, .8f, 1)
        };
        var u = new Uniforms
        {
            Viewport = new(Width, Height, cell, cell),
            Grid = new(sim.Columns, sim.Rows, GlyphCount, count),
            Style = new((float)s.glowIntensity, (float)s.colorVariance, (float)sim.Elapsed, phase / 20),
            TailColor = tail,
            HeadColor = head,
            Title = new(t == null ? -1 : (float)t.Elapsed, TitleSequence.Padded(s.titleText).Length, (float)(t?.Travel ?? 0), (float)(t?.Settled ?? 0)),
            TitleOptions = new(s.titleHideTrails ? 1 : 0, s.titleZoom ? 1 : 0, s.titleEnabled ? 1 : 0, s.seamlessEffects ? 1 : 0),
            TitleLayout = new(titleColumns, lines.Length, sim.Effects.Exists(e => e.Kind == 7) ? 1 : 0, sim.Effects.Exists(e => e.Kind == 9) ? 1 : 0),
            Boot = new(bootCount, s.seamlessEffects || phase < 0 ? 1 : Math.Clamp((phase - 108) / 6, 0, 1), 0, 0),
            TitleMotion = new(t?.Columns ?? 1, t?.Rows ?? 1, s.titleZoom ? (float)(t?.Zoom ?? 1) : 1, (float)s.titleHold),
            TitleLines = new(lines[0].Length, lines.Length > 1 ? lines[1].Length : 0, lines.Length > 2 ? lines[2].Length : 0, (float)(t?.Arrival ?? 1))
        };
        UploadUniform(u);
        Upload(buffers[0], sim.DisplayCells(scratch, s), gridSize);
        Upload(buffers[1], events, 12);
        Upload(buffers[2], drops, drops.Length);
        Upload(buffers[3], lightning, gridSize);
        Upload(buffers[4], mask, gridSize);
        Upload(buffers[5], boot, 8);
        Upload(buffers[6], title, title.Length);
        context.OMSetRenderTargets(target);
        context.RSSetViewport(new Viewport(0, 0, Width, Height));
        context.RSSetState(raster);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(vertex);
        context.PSSetShader(pixel);
        context.PSSetConstantBuffer(0, constants);
        context.PSSetShaderResources(0, views);
        context.PSSetSampler(0, sampler);
        context.Draw(3, 0);
    }
    public void Present()
    {
        swap.Present(1, PresentFlags.None).CheckError();
    }
    public void Capture(string path)
    {
        using var back = swap.GetBuffer<ID3D11Texture2D>(0);
        var d = back.Description;
        d.Usage = ResourceUsage.Staging;
        d.BindFlags = BindFlags.None;
        d.CPUAccessFlags = CpuAccessFlags.Read;
        d.MiscFlags = ResourceOptionFlags.None;
        using var staging = device.CreateTexture2D(d);
        context.CopyResource(staging, back);
        var map = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var bits = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[Width * 4];
                for (int y = 0; y < Height; y++)
                {
                    Marshal.Copy(map.DataPointer + y * (int)map.RowPitch, row, 0, row.Length);
                    Marshal.Copy(row, 0, bits.Scan0 + y * bits.Stride, row.Length);
                }
            }
            finally { bitmap.UnlockBits(bits); }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            bitmap.Save(path, ImageFormat.Png);
        }
        finally { context.Unmap(staging, 0); }
    }
    public void Dispose()
    {
        context.ClearState();
        foreach (var v in views)
            v?.Dispose();
        foreach (var b in buffers)
            b?.Dispose();
        constants.Dispose();
        sampler.Dispose();
        raster.Dispose();
        vertex.Dispose();
        pixel.Dispose();
        target.Dispose();
        swap.Dispose();
        context.Dispose();
        device.Dispose();
    }
}
