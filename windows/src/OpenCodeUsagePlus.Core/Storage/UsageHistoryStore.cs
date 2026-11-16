using System.Text.Json;
using OpenCodeUsagePlus.Core.Models;

namespace OpenCodeUsagePlus.Core.Storage;

public sealed record UsageSample(DateTimeOffset Timestamp, int Rolling, int Weekly, int Monthly);

/// <summary>24-hour rolling history of usage percentages (same JSON shape as the macOS app).</summary>
public sealed class UsageHistoryStore
{
    public const int DefaultMaxSamples = 288;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;
    private readonly int _maxSamples;

    public UsageHistoryStore(string? filePath = null, int maxSamples = DefaultMaxSamples)
    {
        _filePath = filePath ?? AppPaths.HistoryFile;
        _maxSamples = Math.Max(2, maxSamples);
    }

    public static TimeSpan Retention { get; } = TimeSpan.FromHours(24);

    public static UsageSample SampleOf(UsageStats stats, DateTimeOffset now) =>
        new(now, stats.Rolling.DisplayPercent, stats.Weekly.DisplayPercent, stats.Monthly.DisplayPercent);

    public IReadOnlyList<UsageSample> Load(DateTimeOffset now)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<UsageSample>();
            }

            var samples = JsonSerializer.Deserialize<List<UsageSample>>(File.ReadAllText(_filePath), JsonOptions);
            return samples is null ? Array.Empty<UsageSample>() : Trim(samples, now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLog.Warn($"history load failed: {ex.Message}");
            return Array.Empty<UsageSample>();
        }
    }

    /// <summary>
    /// Appends a sample. Samples closer than a minute replace the previous one so manual refreshes do
    /// not create spikes.
    /// </summary>
    public IReadOnlyList<UsageSample> Append(
        IReadOnlyList<UsageSample> history,
        UsageSample sample,
        DateTimeOffset now)
    {
        var samples = history.ToList();
        if (samples.Count > 0 && Math.Abs((samples[^1].Timestamp - sample.Timestamp).TotalSeconds) < 60)
        {
            samples[^1] = sample;
        }
        else
        {
            samples.Add(sample);
        }

        return Trim(samples, now);
    }

    public void Save(IReadOnlyList<UsageSample> samples)
    {
        try
        {
            AppPaths.EnsureExists();
            var temp = _filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(samples, JsonOptions));
            File.Move(temp, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"history save failed: {ex.Message}");
        }
    }

    private IReadOnlyList<UsageSample> Trim(IReadOnlyList<UsageSample> samples, DateTimeOffset now)
    {
        var cutoff = now - Retention;
        var ordered = samples.Where(sample => sample.Timestamp >= cutoff)
            .OrderBy(sample => sample.Timestamp)
            .ToList();

        if (ordered.Count > _maxSamples)
        {
            ordered = ordered.Skip(ordered.Count - _maxSamples).ToList();
        }

        return ordered;
    }
}
