namespace OpenCodeUsagePlus;

/// <summary>Process-wide switches driven by command-line flags or environment variables.</summary>
internal static class AppEnvironment
{
    /// <summary>
    /// <c>--safe-mode</c> / <c>OPENCODE_USAGE_PLAIN_WINDOW=1</c>: render the flyout as a plain
    /// (non-layered) window without the drop shadow. Used to isolate WPF/GPU rendering crashes.
    /// </summary>
    public static bool PlainWindow { get; set; }
}
