namespace OpenCodeUsagePlus.Core.Utilities;

public static class DurationFormat
{
    /// <summary>Seconds left until <paramref name="target"/>, clamped at zero.</summary>
    public static int SecondsUntil(DateTimeOffset target, DateTimeOffset now) =>
        (int)Math.Max(0, Math.Floor((target - now).TotalSeconds));

    /// <summary>Compact duration: "2d 3h", "4h 12m", "7m", "&lt; 1m".</summary>
    public static string Format(TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
        {
            return "< 1m";
        }

        var days = (int)span.TotalDays;
        var hours = span.Hours;
        var minutes = span.Minutes;

        var parts = new List<string>(3);
        if (days > 0)
        {
            parts.Add($"{days}d");
        }

        if (hours > 0)
        {
            parts.Add($"{hours}h");
        }

        if (minutes > 0)
        {
            parts.Add($"{minutes}m");
        }

        return parts.Count == 0 ? "< 1m" : string.Join(" ", parts);
    }

    public static string Format(int seconds) => Format(TimeSpan.FromSeconds(seconds));

    public static string FormatUntil(DateTimeOffset target, DateTimeOffset now) =>
        Format(SecondsUntil(target, now));
}
