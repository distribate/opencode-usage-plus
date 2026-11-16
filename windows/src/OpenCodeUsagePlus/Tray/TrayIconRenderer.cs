using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCodeUsagePlus.Core.Models;

namespace OpenCodeUsagePlus.Tray;

/// <summary>
/// Draws the tray bitmap at the size the shell asks for (16 px at 100 %, 24 at 150 %, 32 at 200 %).
/// <c>Icon.FromHandle</c> does not own the HICON, so every icon is cloned and the handle destroyed.
/// </summary>
internal static class TrayIconRenderer
{
    private static readonly Color Safe = Color.FromArgb(0x2F, 0x9E, 0x58);
    private static readonly Color Elevated = Color.FromArgb(0xE0, 0xA1, 0x00);
    private static readonly Color Critical = Color.FromArgb(0xE5, 0x48, 0x4D);

    public static Icon Render(UsageStats? stats, TrayIconStyle style, bool lightTaskbar, int size)
    {
        size = Math.Clamp(size, 16, 64);

        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            switch (style)
            {
                case TrayIconStyle.Stack:
                    DrawStack(graphics, stats, size, lightTaskbar);
                    break;
                case TrayIconStyle.Logo:
                    DrawBolt(graphics, stats, size, lightTaskbar);
                    break;
                default:
                    DrawRing(graphics, stats, size, lightTaskbar);
                    break;
            }
        }

        return ToIcon(bitmap);
    }

    public static Color ColorForLevel(double percent) => UsageLevels.FromPercent(percent) switch
    {
        UsageLevel.Safe => Safe,
        UsageLevel.Elevated => Elevated,
        _ => Critical,
    };

    private static void DrawRing(Graphics graphics, UsageStats? stats, int size, bool lightTaskbar)
    {
        var thickness = Math.Max(2f, size * 0.17f);
        var inset = thickness / 2f + Math.Max(1f, size * 0.08f);
        var rect = new RectangleF(inset, inset, size - (inset * 2), size - (inset * 2));

        using var track = new Pen(TrackColor(lightTaskbar), thickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawArc(track, rect, 0, 359.9f);

        if (stats is null)
        {
            // Keep the idle icon visible at 16 px instead of a nearly invisible empty ring.
            var dot = Math.Max(2f, size * 0.16f);
            using var dotBrush = new SolidBrush(TrackColor(lightTaskbar));
            graphics.FillEllipse(dotBrush, (size - dot) / 2f, (size - dot) / 2f, dot, dot);
            return;
        }

        var percent = Math.Clamp(stats.MaxPercent, 0, 100);
        if (percent <= 0)
        {
            return;
        }

        using var arc = new Pen(ColorForLevel(percent), thickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawArc(arc, rect, -90, (float)Math.Min(percent * 3.6, 359.9));
    }

    private static void DrawStack(Graphics graphics, UsageStats? stats, int size, bool lightTaskbar)
    {
        var padding = size * 0.12f;
        var width = size - (padding * 2);
        var barHeight = Math.Max(1.6f, size * 0.16f);
        var gap = Math.Max(1f, size * 0.10f);
        var total = (barHeight * 3) + (gap * 2);
        var y = (size - total) / 2f;

        var values = stats is null
            ? new[] { 0d, 0d, 0d }
            : new[] { stats.Rolling.Percent, stats.Weekly.Percent, stats.Monthly.Percent };

        using var trackBrush = new SolidBrush(TrackColor(lightTaskbar));
        foreach (var value in values)
        {
            var barRect = new RectangleF(padding, y, width, barHeight);
            using (var track = Rounded(barRect, barHeight / 2f))
            {
                graphics.FillPath(trackBrush, track);
            }

            var filled = (float)(width * (Math.Clamp(value, 0, 100) / 100.0));
            if (filled > 0.4f)
            {
                filled = Math.Max(filled, barHeight);
                var fillRect = new RectangleF(padding, y, Math.Min(filled, width), barHeight);
                using var fill = Rounded(fillRect, barHeight / 2f);
                using var brush = new SolidBrush(ColorForLevel(value));
                graphics.FillPath(brush, fill);
            }

            y += barHeight + gap;
        }
    }

    private static void DrawBolt(Graphics graphics, UsageStats? stats, int size, bool lightTaskbar)
    {
        var points = new[]
        {
            new PointF(0.56f, 0.05f),
            new PointF(0.17f, 0.56f),
            new PointF(0.44f, 0.56f),
            new PointF(0.35f, 0.95f),
            new PointF(0.83f, 0.41f),
            new PointF(0.54f, 0.41f),
        };

        var scaled = new PointF[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            scaled[i] = new PointF(points[i].X * size, points[i].Y * size);
        }

        var color = stats is null
            ? (lightTaskbar ? Color.FromArgb(0x1B, 0x1B, 0x1D) : Color.FromArgb(0xF2, 0xF2, 0xF4))
            : ColorForLevel(stats.MaxPercent);

        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, scaled);
    }

    private static Color TrackColor(bool lightTaskbar) =>
        lightTaskbar ? Color.FromArgb(60, 0, 0, 0) : Color.FromArgb(70, 255, 255, 255);

    private static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
        if (diameter <= 0.5f)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Icon ToIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        if (handle == IntPtr.Zero)
        {
            return SystemIcons.Application;
        }

        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
