namespace OpenCodeUsagePlus.Core.Services;

public enum UsageErrorKind
{
    NotConfigured,
    AuthExpired,
    Forbidden,
    Http,
    Network,
    Parse,
}

public sealed class UsageApiException : Exception
{
    public UsageApiException(UsageErrorKind kind, string? message = null, Exception? inner = null)
        : base(message ?? DefaultMessage(kind), inner) => Kind = kind;

    public UsageErrorKind Kind { get; }

    /// <summary>Text safe to show in a 300 px popup.</summary>
    public string UserMessage => Kind switch
    {
        UsageErrorKind.NotConfigured =>
            "OpenCode is not signed in. Run \"opencode auth login\", or paste an API key in Settings.",
        UsageErrorKind.AuthExpired =>
            "Authentication expired. Start OpenCode once so it can refresh the token, or paste an API key.",
        UsageErrorKind.Forbidden =>
            "Access denied (403). An active OpenCode Go plan is required, and the token may have expired.",
        UsageErrorKind.Network => "Network error — check your connection.",
        UsageErrorKind.Parse => "Unexpected response from the usage API.",
        _ => Message,
    };

    private static string DefaultMessage(UsageErrorKind kind) => kind switch
    {
        UsageErrorKind.NotConfigured => "No OpenCode credential available.",
        UsageErrorKind.AuthExpired => "Authentication expired.",
        UsageErrorKind.Forbidden => "Access denied.",
        UsageErrorKind.Network => "Network error.",
        UsageErrorKind.Parse => "Failed to parse the usage response.",
        _ => "Usage request failed.",
    };
}
