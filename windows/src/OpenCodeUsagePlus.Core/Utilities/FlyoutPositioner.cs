namespace OpenCodeUsagePlus.Core.Utilities;

/// <summary>Integer rectangle, unit-agnostic (DIPs in the shell, pixels in tests).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

/// <summary>
/// Places the flyout near the tray icon. The working area must already exclude the taskbar and be
/// expressed in the same units as the window size (DIPs).
/// </summary>
public static class FlyoutPositioner
{
    public const int Margin = 8;

    /// <summary>A cursor within this distance of an edge means the taskbar sits on that edge.</summary>
    public const int EdgeThreshold = 64;

    public static (int X, int Y) Place(int width, int height, int cursorX, int cursorY, PixelRect workArea)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return (cursorX, cursorY);
        }

        var x = Clamp(cursorX - width / 2, workArea.X + Margin, workArea.Right - width - Margin);

        int y;
        if (cursorY >= workArea.Bottom - EdgeThreshold)
        {
            // Taskbar at the bottom.
            y = workArea.Bottom - height - Margin;
        }
        else if (cursorY <= workArea.Y + EdgeThreshold)
        {
            // Taskbar at the top.
            y = workArea.Y + Margin;
        }
        else
        {
            // Side taskbar or a floating click: centre on the cursor, then clamp.
            y = cursorY - height / 2;
        }

        y = Clamp(y, workArea.Y + Margin, workArea.Bottom - height - Margin);
        return (x, y);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
