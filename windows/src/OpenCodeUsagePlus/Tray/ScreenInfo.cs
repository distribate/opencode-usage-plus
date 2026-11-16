using System;
using System.Runtime.InteropServices;
using OpenCodeUsagePlus.Core.Utilities;

namespace OpenCodeUsagePlus.Tray;

/// <summary>
/// Converts the physical cursor position and the monitor working area into DIPs, so the flyout can
/// be positioned correctly on mixed-DPI multi-monitor setups (100 %, 150 %, 200 %).
/// </summary>
internal static class ScreenInfo
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;

    public static (PixelRect WorkArea, int CursorX, int CursorY) Describe(System.Drawing.Point physicalCursor)
    {
        var scale = 1.0;

        var monitor = MonitorFromPoint(new NativePoint { X = physicalCursor.X, Y = physicalCursor.Y }, MonitorDefaultToNearest);
        if (monitor != IntPtr.Zero)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                if (GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0)
                {
                    scale = dpiX / 96.0;
                }

                var width = info.Work.Right - info.Work.Left;
                var height = info.Work.Bottom - info.Work.Top;
                return (
                    new PixelRect(
                        (int)Math.Round(info.Work.Left / scale),
                        (int)Math.Round(info.Work.Top / scale),
                        (int)Math.Round(width / scale),
                        (int)Math.Round(height / scale)),
                    (int)Math.Round(physicalCursor.X / scale),
                    (int)Math.Round(physicalCursor.Y / scale));
            }
        }

        // Fallback: the WinForms screen API (primary/monitor DPI unaware, but always available).
        var area = System.Windows.Forms.Screen.FromPoint(physicalCursor).WorkingArea;
        return (
            new PixelRect(area.X, area.Y, area.Width, area.Height),
            physicalCursor.X,
            physicalCursor.Y);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
