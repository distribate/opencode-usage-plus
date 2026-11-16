using System.Text.Json;
using OpenCodeUsagePlus.Core.Models;

namespace OpenCodeUsagePlus.Core.Storage;

/// <summary>
/// Parses the credential JSON that OpenCode writes into <c>credential.value</c> (and, historically,
/// into <c>auth.json</c>). Tolerant by design: the schema already changed once
/// (<c>OAuth</c> / <c>Key</c> / <c>External</c> union, legacy bare shapes).
/// </summary>
public static class CredentialJson
{
    public static AuthCredential? TryParse(string? raw, string? sourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var node = root;
            if (TryGetProperty(root, "opencode", out var opencode) && opencode.ValueKind == JsonValueKind.Object)
            {
                // Legacy auth.json: { "opencode": { "access": "...", "refresh": "..." } }
                node = opencode;
            }
            else if (TryGetProperty(root, "credentials", out var list)
                     && list.ValueKind == JsonValueKind.Array
                     && list.GetArrayLength() > 0
                     && list[0].ValueKind == JsonValueKind.Object)
            {
                node = list[0];
            }

            var access = GetString(node, "access");
            var key = GetString(node, "key") ?? GetString(node, "token");
            var token = access ?? key;
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var type = GetString(node, "type");
            var kind = type?.ToLowerInvariant() switch
            {
                "external" => CredentialKind.External,
                "key" or "api" or "apikey" => CredentialKind.ApiKey,
                "oauth" => CredentialKind.OAuth,
                _ => access is not null ? CredentialKind.OAuth : CredentialKind.ApiKey,
            };

            if (kind == CredentialKind.External)
            {
                // No local secret; the token came from an external manager we cannot read.
                return null;
            }

            string? organizationId = null;
            string? organizationName = null;
            if (TryGetProperty(node, "metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
            {
                organizationId = GetString(metadata, "orgID") ?? GetString(metadata, "orgId");
                organizationName = GetString(metadata, "orgName");
            }

            organizationId ??= GetString(node, "orgID") ?? GetString(node, "orgId");

            return new AuthCredential(token.Trim(), kind, organizationId, organizationName, sourcePath);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = property.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
