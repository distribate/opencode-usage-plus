using System;
using System.IO;
using System.Linq;
using OpenCodeUsagePlus.Core.Models;
using OpenCodeUsagePlus.Core.Storage;
using Xunit;

namespace OpenCodeUsagePlus.Tests;

public sealed class UsageHistoryStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _file;

    public UsageHistoryStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ocu-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _file = Path.Combine(_directory, "history.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best effort.
        }
    }

    [Fact]
    public void Appends_replaces_within_a_minute_and_round_trips()
    {
        var store = new UsageHistoryStore(_file);
        var now = DateTimeOffset.Parse("2026-11-16T12:00:00Z");

        var history = store.Append(Array.Empty<UsageSample>(), new UsageSample(now, 10, 20, 30), now);
        history = store.Append(history, new UsageSample(now.AddSeconds(10), 11, 21, 31), now);
        Assert.Single(history);
        Assert.Equal(11, history[0].Rolling);

        history = store.Append(history, new UsageSample(now.AddMinutes(6), 12, 22, 32), now.AddMinutes(6));
        Assert.Equal(2, history.Count);

        store.Save(history);

        var reloaded = store.Load(now.AddMinutes(6));
        Assert.Equal(2, reloaded.Count);
        Assert.Equal(12, reloaded[^1].Rolling);
        Assert.Equal(22, reloaded[^1].Weekly);
        Assert.Equal(32, reloaded[^1].Monthly);
    }

    [Fact]
    public void Drops_samples_older_than_24_hours()
    {
        var store = new UsageHistoryStore(_file);
        var now = DateTimeOffset.Parse("2026-11-16T12:00:00Z");
        var history = new[]
        {
            new UsageSample(now.AddHours(-25), 1, 1, 1),
            new UsageSample(now.AddHours(-2), 2, 2, 2),
        };

        var trimmed = store.Append(history, new UsageSample(now, 3, 3, 3), now);

        Assert.Equal(2, trimmed.Count);
        Assert.DoesNotContain(trimmed, sample => sample.Rolling == 1);
    }

    [Fact]
    public void Caps_the_sample_count()
    {
        var store = new UsageHistoryStore(_file, maxSamples: 3);
        var now = DateTimeOffset.Parse("2026-11-16T12:00:00Z");
        var history = Enumerable.Range(0, 5)
            .Select(i => new UsageSample(now.AddMinutes(-10 + i), i, i, i))
            .ToArray();

        var trimmed = store.Append(history, new UsageSample(now, 9, 9, 9), now);

        Assert.Equal(3, trimmed.Count);
        Assert.Equal(9, trimmed[^1].Rolling);
    }

    [Fact]
    public void Sample_of_uses_display_percentages()
    {
        var stats = new UsageStats(
            new UsageItem("ok", 41.6, DateTimeOffset.UtcNow),
            new UsageItem("ok", 60.2, DateTimeOffset.UtcNow),
            new UsageItem("ok", 99.9, DateTimeOffset.UtcNow));

        var sample = UsageHistoryStore.SampleOf(stats, DateTimeOffset.UtcNow);

        Assert.Equal(42, sample.Rolling);
        Assert.Equal(60, sample.Weekly);
        Assert.Equal(100, sample.Monthly);
    }
}
