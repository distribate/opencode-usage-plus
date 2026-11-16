using OpenCodeUsagePlus.Core.Models;
using OpenCodeUsagePlus.Core.Storage;
using Xunit;

namespace OpenCodeUsagePlus.Tests;

public sealed class CredentialJsonTests
{
    [Fact]
    public void Parses_the_current_oauth_shape()
    {
        const string json = """
        {
          "type": "oauth",
          "access": "at-123",
          "refresh": "rt-456",
          "expires": 1763000000000,
          "metadata": { "orgID": "wrk_abc", "orgName": "Acme" }
        }
        """;

        var credential = CredentialJson.TryParse(json, "opencode.db");

        Assert.NotNull(credential);
        Assert.Equal("at-123", credential!.Token);
        Assert.Equal(CredentialKind.OAuth, credential.Kind);
        Assert.Equal("wrk_abc", credential.OrganizationId);
        Assert.Equal("Acme", credential.OrganizationName);
        Assert.Equal("opencode.db", credential.SourcePath);
    }

    [Fact]
    public void Parses_a_saved_api_key()
    {
        var credential = CredentialJson.TryParse("""{"type":"key","key":"sk-test-123"}""");

        Assert.NotNull(credential);
        Assert.Equal("sk-test-123", credential!.Token);
        Assert.Equal(CredentialKind.ApiKey, credential.Kind);
    }

    [Fact]
    public void Parses_the_legacy_auth_json_wrapper()
    {
        var credential = CredentialJson.TryParse("""
        { "opencode": { "type": "oauth", "access": "legacy-token", "refresh": "r" } }
        """);

        Assert.NotNull(credential);
        Assert.Equal("legacy-token", credential!.Token);
        Assert.Equal(CredentialKind.OAuth, credential.Kind);
    }

    [Fact]
    public void Org_id_is_case_insensitive_and_optional()
    {
        var withLowercase = CredentialJson.TryParse("""{"type":"oauth","access":"a","metadata":{"orgId":"wrk_lower"}}""");
        var without = CredentialJson.TryParse("""{"type":"oauth","access":"a"}""");

        Assert.Equal("wrk_lower", withLowercase!.OrganizationId);
        Assert.Null(without!.OrganizationId);
    }

    [Fact]
    public void External_credentials_are_unusable() =>
        Assert.Null(CredentialJson.TryParse("""{"type":"external","metadata":{"orgID":"wrk_x"}}"""));

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("""{"type":"oauth"}""")]
    public void Malformed_values_return_null(string json) =>
        Assert.Null(CredentialJson.TryParse(json));
}
