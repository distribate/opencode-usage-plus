namespace OpenCodeUsagePlus.Core.Models;

public enum UsageLevel
{
    Safe,
    Elevated,
    Critical,
}

public static class UsageLevels
{
    /// <summary>Shared with the macOS app: &lt;51 safe, &lt;81 elevated, otherwise critical.</summary>
    public static UsageLevel FromPercent(double percent) =>
        percent < 51 ? UsageLevel.Safe
        : percent < 81 ? UsageLevel.Elevated
        : UsageLevel.Critical;
}

public sealed record UsageItem(string Status, double Percent, DateTimeOffset ResetsAt)
{
    public int DisplayPercent => (int)Math.Round(Percent, MidpointRounding.AwayFromZero);

    public bool IsRateLimited => string.Equals(Status, "rate-limited", StringComparison.OrdinalIgnoreCase);
}

public sealed record UsageStats(UsageItem Rolling, UsageItem Weekly, UsageItem Monthly)
{
    /// <summary>The most constrained window — what a single-number tray icon should show.</summary>
    public double MaxPercent => Math.Max(Rolling.Percent, Math.Max(Weekly.Percent, Monthly.Percent));

    public UsageItem MaxItem =>
        Rolling.Percent >= Weekly.Percent && Rolling.Percent >= Monthly.Percent ? Rolling
        : Weekly.Percent >= Monthly.Percent ? Weekly
        : Monthly;
}

public enum TrayIconStyle
{
    /// <summary>Progress ring for the busiest window.</summary>
    Ring,

    /// <summary>Three stacked bars, one per window.</summary>
    Stack,

    /// <summary>Bolt tinted by the busiest window's level.</summary>
    Logo,
}
