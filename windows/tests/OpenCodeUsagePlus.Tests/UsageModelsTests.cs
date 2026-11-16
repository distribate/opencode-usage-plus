using System;
using OpenCodeUsagePlus.Core.Models;
using Xunit;

namespace OpenCodeUsagePlus.Tests;

public sealed class UsageModelsTests
{
    [Theory]
    [InlineData(0, UsageLevel.Safe)]
    [InlineData(50, UsageLevel.Safe)]
    [InlineData(50.9, UsageLevel.Safe)]
    [InlineData(51, UsageLevel.Elevated)]
    [InlineData(80, UsageLevel.Elevated)]
    [InlineData(81, UsageLevel.Critical)]
    [InlineData(100, UsageLevel.Critical)]
    public void Levels_match_the_macos_thresholds(double percent, UsageLevel expected) =>
        Assert.Equal(expected, UsageLevels.FromPercent(percent));

    [Fact]
    public void Display_percent_rounds_half_away_from_zero()
    {
        var item = new UsageItem("ok", 42.5, DateTimeOffset.UtcNow);
        Assert.Equal(43, item.DisplayPercent);
    }

    [Fact]
    public void Max_item_picks_the_most_constrained_window()
    {
        var stats = new UsageStats(
            new UsageItem("ok", 40, DateTimeOffset.UtcNow),
            new UsageItem("ok", 92, DateTimeOffset.UtcNow),
            new UsageItem("ok", 61, DateTimeOffset.UtcNow));

        Assert.Equal(92, stats.MaxPercent);
        Assert.Equal(92, stats.MaxItem.Percent);
    }

    [Fact]
    public void Rate_limited_status_is_detected_case_insensitively()
    {
        Assert.True(new UsageItem("rate-limited", 100, DateTimeOffset.UtcNow).IsRateLimited);
        Assert.False(new UsageItem("ok", 10, DateTimeOffset.UtcNow).IsRateLimited);
    }
}
