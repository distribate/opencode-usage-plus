using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using OpenCodeUsagePlus.Core.Models;
using OpenCodeUsagePlus.Core.Storage;

namespace OpenCodeUsagePlus.Views;

/// <summary>24-hour sparkline of monthly usage (same data as the macOS app's SparklineView).</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples),
        typeof(IReadOnlyList<UsageSample>),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentPercentProperty = DependencyProperty.Register(
        nameof(CurrentPercent),
        typeof(double),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush SafeBrush = Frozen(0x2F, 0x9E, 0x58);
    private static readonly Brush ElevatedBrush = Frozen(0xE0, 0xA1, 0x00);
    private static readonly Brush CriticalBrush = Frozen(0xE5, 0x48, 0x4D);

    public IReadOnlyList<UsageSample>? Samples
    {
        get => (IReadOnlyList<UsageSample>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public double CurrentPercent
    {
        get => (double)GetValue(CurrentPercentProperty);
        set => SetValue(CurrentPercentProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var samples = Samples;
        var width = ActualWidth;
        var height = ActualHeight;
        if (samples is null || samples.Count < 2 || width <= 0 || height <= 0)
        {
            return;
        }

        var baseline = TrackBrush ?? Brushes.Gray;
        var baselinePen = new Pen(baseline, 1);
        baselinePen.Freeze();
        drawingContext.DrawLine(baselinePen, new Point(0, height - 0.5), new Point(width, height - 0.5));

        var ordered = samples.OrderBy(sample => sample.Timestamp).ToList();
        var first = ordered[0].Timestamp;
        var last = ordered[^1].Timestamp;
        var span = (last - first).TotalSeconds;
        if (span <= 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var started = false;
            foreach (var sample in ordered)
            {
                var progress = (sample.Timestamp - first).TotalSeconds / span;
                var x = width * Math.Clamp(progress, 0, 1);
                var y = height - (Math.Clamp(sample.Monthly, 0, 100) / 100.0 * height);
                var point = new Point(x, y);
                if (!started)
                {
                    context.BeginFigure(point, isFilled: false, isClosed: false);
                    started = true;
                }
                else
                {
                    context.LineTo(point, isStroked: true, isSmoothJoin: true);
                }
            }
        }

        geometry.Freeze();

        var pen = new Pen(BrushForLevel(CurrentPercent), 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();

        drawingContext.DrawGeometry(null, pen, geometry);
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
