using System;
using System.Windows;
using System.Windows.Media;
using OpenCodeUsagePlus.Core.Models;

namespace OpenCodeUsagePlus.Views;

/// <summary>Rounded progress bar with the shared usage threshold colours.</summary>
public sealed class UsageBar : FrameworkElement
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent),
        typeof(double),
        typeof(UsageBar),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(UsageBar),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush SafeBrush = Frozen(0x2F, 0x9E, 0x58);
    private static readonly Brush ElevatedBrush = Frozen(0xE0, 0xA1, 0x00);
    private static readonly Brush CriticalBrush = Frozen(0xE5, 0x48, 0x4D);

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var radius = height / 2;
        drawingContext.DrawRoundedRectangle(
            TrackBrush ?? Brushes.Gray,
            null,
            new Rect(0, 0, width, height),
            radius,
            radius);

        var fraction = Math.Clamp(Percent, 0, 100) / 100.0;
        if (fraction <= 0)
        {
            return;
        }

        var fillWidth = Math.Min(width, Math.Max(width * fraction, height));
        drawingContext.DrawRoundedRectangle(
            BrushForLevel(Percent),
            null,
            new Rect(0, 0, fillWidth, height),
            radius,
            radius);
    }

    private static Brush BrushForLevel(double percent) => UsageLevels.FromPercent(percent) switch
    {
        UsageLevel.Safe => SafeBrush,
        UsageLevel.Elevated => ElevatedBrush,
        _ => CriticalBrush,
    };

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
