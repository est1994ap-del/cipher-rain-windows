using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
namespace CipherRain;
public static class Native
{
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    public delegate bool EnumProc(IntPtr hwnd, IntPtr data);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct PowerStatus
    {
        public byte AC, Battery, Percent, Saver; public int Life, FullLife;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern uint MapWindowPoints(IntPtr from, IntPtr to, ref POINT point, uint count);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out PowerStatus status);
    [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateWaitableTimerEx(IntPtr attributes, string? name, uint flags, uint access);
    [DllImport("kernel32.dll")] public static extern bool SetWaitableTimer(IntPtr timer, ref long due, int period, IntPtr completion, IntPtr argument, bool resume);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
    [DllImport("wtsapi32.dll")] public static extern bool WTSRegisterSessionNotification(IntPtr hwnd, int flags);
    [DllImport("wtsapi32.dll")] public static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr RegisterPowerSettingNotification(IntPtr hwnd, ref Guid guid, uint flags);
    [DllImport("user32.dll")] public static extern bool UnregisterPowerSettingNotification(IntPtr handle);
    public static string Class(IntPtr hwnd)
    {
        var s = new StringBuilder(256);
        GetClassName(hwnd, s, 256);
        return s.ToString();
    }
    public static IntPtr DesktopParent()
    {
        var prog = FindWindow("Progman", null);
        if (prog == IntPtr.Zero)
            return IntPtr.Zero;
        SendMessageTimeout(prog, 0x052c, IntPtr.Zero, IntPtr.Zero, 2, 1000, out _);
        IntPtr worker = IntPtr.Zero;
        EnumWindows((top, _) => { if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) { worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null); return false; } return true; }, IntPtr.Zero);
        if (worker == IntPtr.Zero)
            worker = FindWindowEx(prog, IntPtr.Zero, "WorkerW", null);
        return worker;
    }
    public static bool AttachDesktop(IntPtr hwnd, IntPtr parent, Rectangle bounds)
    {
        if (parent == IntPtr.Zero || !IsWindow(parent))
            return false;
        var style = GetWindowLongPtr(hwnd, -16).ToInt64();
        SetWindowLongPtr(hwnd, -16, new((style & ~0x80000000L) | 0x40000000L | 0x10000000L));
        SetWindowLongPtr(hwnd, -20, new(0x08000000L | 0x80L | 0x20L));
        SetParent(hwnd, parent);
        var point = new POINT { X = bounds.X, Y = bounds.Y };
        MapWindowPoints(IntPtr.Zero, parent, ref point, 1);
        return SetWindowPos(hwnd, IntPtr.Zero, point.X, point.Y, bounds.Width, bounds.Height, 0x10 | 0x40 | 0x20) && GetParent(hwnd) == parent;
    }
    public static bool Covered(Rectangle area)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero)
            return false;
        var cls = Class(fg);
        if (cls == "Progman" || cls == "WorkerW")
            return false;
        if (!GetWindowRect(fg, out var r))
            return false;
        return r.Left <= area.Left && r.Top <= area.Top && r.Right >= area.Right && r.Bottom >= area.Bottom;
    }
}
