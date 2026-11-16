using System;
using OpenCodeUsagePlus.Core.Utilities;
using Xunit;

namespace OpenCodeUsagePlus.Tests;

public sealed class DurationFormatTests
{
    [Fact]
    public void Formats_days_hours_and_minutes()
    {
        Assert.Equal("2d 3h 4m", DurationFormat.Format(new TimeSpan(2, 3, 4, 0)));
        Assert.Equal("5h", DurationFormat.Format(TimeSpan.FromHours(5)));
        Assert.Equal("7m", DurationFormat.Format(TimeSpan.FromMinutes(7)));
    }

    [Fact]
    public void Under_a_minute_is_labelled() =>
        Assert.Equal("< 1m", DurationFormat.Format(TimeSpan.FromSeconds(30)));

    [Fact]
    public void Past_targets_are_clamped()
    {
        var now = DateTimeOffset.Parse("2026-11-16T12:00:00Z");
        var target = now.AddMinutes(-5);
        Assert.Equal(0, DurationFormat.SecondsUntil(target, now));
        Assert.Equal("< 1m", DurationFormat.FormatUntil(target, now));
    }

    [Fact]
    public void Seconds_are_truncated()
    {
        var now = DateTimeOffset.Parse("2026-11-16T12:00:00Z");
        Assert.Equal(59, DurationFormat.SecondsUntil(now.AddSeconds(59.9), now));
    }
}
