namespace OpenCodeUsagePlus.Core.Models;

public enum CredentialKind
{
    OAuth,
    ApiKey,
    External,
}

/// <summary>A usable secret plus the workspace hint OpenCode stores next to it.</summary>
/// <param name="Token">Bearer token. Never log this value.</param>
/// <param name="SourcePath">Where the token came from, for the Settings screen. Never contains the token.</param>
public sealed record AuthCredential(
    string Token,
    CredentialKind Kind,
    string? OrganizationId = null,
    string? OrganizationName = null,
    string? SourcePath = null)
{
    public override string ToString() => $"AuthCredential(kind={Kind}, source={SourcePath ?? "unknown"})";
}
