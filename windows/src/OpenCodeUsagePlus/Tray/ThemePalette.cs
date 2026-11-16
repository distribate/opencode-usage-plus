using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace OpenCodeUsagePlus.Tray;

/// <summary>
/// Minimal palette reader. WPF has no built-in dark theme, so the flyout resolves its brushes from
/// the Windows personalization keys at startup and whenever the user preference changes.
/// </summary>
internal sealed record ThemePalette(
    bool IsLight,
    Color Window,
    Color Card,
    Color Text,
    Color Subtle,
    Color Divider,
    Color Track)
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static ThemePalette Current { get; private set; } = Load();

    /// <summary>Taskbar icons sit on the taskbar, which follows a different flag than app windows.</summary>
    public static bool TaskbarIsLight => ReadDword("SystemUsesLightTheme") != 0;

    public static void Refresh() => Current = Load();

    public static ThemePalette Load()
    {
        var light = ReadDword("AppsUseLightTheme") != 0;
        return light
            ? new ThemePalette(
                IsLight: true,
                Window: Color.FromRgb(0xFA, 0xFA, 0xFB),
                Card: Color.FromRgb(0xF1, 0xF1, 0xF4),
                Text: Color.FromRgb(0x1B, 0x1B, 0x1D),
                Subtle: Color.FromRgb(0x6A, 0x6A, 0x70),
                Divider: Color.FromRgb(0xE1, 0xE1, 0xE5),
                Track: Color.FromArgb(0x22, 0x00, 0x00, 0x00))
            : new ThemePalette(
                IsLight: false,
                Window: Color.FromRgb(0x20, 0x20, 0x22),
                Card: Color.FromRgb(0x2B, 0x2B, 0x2E),
                Text: Color.FromRgb(0xF2, 0xF2, 0xF4),
                Subtle: Color.FromRgb(0xA0, 0xA0, 0xA8),
                Divider: Color.FromRgb(0x3A, 0x3A, 0x3E),
                Track: Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    }

    public static void Apply(ResourceDictionary resources)
    {
        var palette = Current;
        resources["WindowBrush"] = Frozen(palette.Window);
        resources["CardBrush"] = Frozen(palette.Card);
        resources["TextBrush"] = Frozen(palette.Text);
        resources["SubtleBrush"] = Frozen(palette.Subtle);
        resources["DividerBrush"] = Frozen(palette.Divider);
        resources["TrackBrush"] = Frozen(palette.Track);
        resources["WarningBrush"] = Frozen(Color.FromRgb(0xD8, 0x9B, 0x00));
        resources["SuccessBrush"] = Frozen(Color.FromRgb(0x2F, 0x9E, 0x58));
        resources["HoverBrush"] = Frozen(Color.FromArgb(palette.IsLight ? (byte)0x14 : (byte)0x22, 0x80, 0x80, 0x80));
    }

    private static int ReadDword(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int value ? value : 1;
        }
        catch
        {
            return 1;
        }
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
