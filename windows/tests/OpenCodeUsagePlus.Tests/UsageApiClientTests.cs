using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenCodeUsagePlus.Core.Models;
using OpenCodeUsagePlus.Core.Services;
using Xunit;

namespace OpenCodeUsagePlus.Tests;

public sealed class UsageApiClientTests
{
    private static readonly Uri Endpoint = new("https://usage.test/zen/go/v1/usage");

    private static readonly AuthCredential Credential =
        new("at-token", CredentialKind.OAuth, OrganizationId: "wrk_123");

    [Fact]
    public async Task Parses_the_usage_payload()
    {
        const string body = """
        {
          "usage": {
            "rolling": { "status": "ok", "percent": 42, "resetsAt": "2026-11-16T15:00:00.123Z" },
            "weekly":  { "status": "ok", "percent": 61.5, "resetsAt": "2026-11-19T00:00:00Z" },
            "monthly": { "status": "rate-limited", "percent": 100, "resetsAt": "2026-12-01T00:00:00+00:00" }
          }
        }
        """;

        using var handler = new StubHandler(HttpStatusCode.OK, body);
        using var client = new UsageApiClient(Endpoint, new HttpClient(handler));

        var stats = await client.FetchAsync(Credential);

        Assert.Equal(42, stats.Rolling.Percent);
        Assert.Equal(61.5, stats.Weekly.Percent);
        Assert.True(stats.Monthly.IsRateLimited);
        Assert.Equal(100, stats.MaxPercent);
        Assert.Equal(2026, stats.Rolling.ResetsAt.Year);
    }

    [Fact]
    public async Task Sends_the_bearer_token_and_the_workspace_header()
    {
        const string body = """
        {"usage":{"rolling":{"status":"ok","percent":1,"resetsAt":"2026-11-16T15:00:00Z"},
        "weekly":{"status":"ok","percent":2,"resetsAt":"2026-11-16T15:00:00Z"},
        "monthly":{"status":"ok","percent":3,"resetsAt":"2026-11-16T15:00:00Z"}}}
        """;

        using var handler = new StubHandler(HttpStatusCode.OK, body);
        using var client = new UsageApiClient(Endpoint, new HttpClient(handler));

        await client.FetchAsync(Credential);

        Assert.Equal("Bearer at-token", handler.Authorization);
        Assert.Equal("wrk_123", handler.OrganizationId);
        Assert.Contains("OpenCodeUsagePlus", handler.UserAgent);
    }

    [Fact]
    public async Task Omits_the_workspace_header_when_unknown()
    {
        using var handler = new StubHandler(HttpStatusCode.OK, MinimalBody);
        using var client = new UsageApiClient(Endpoint, new HttpClient(handler));

        await client.FetchAsync(new AuthCredential("sk-key", CredentialKind.ApiKey));

        Assert.Null(handler.OrganizationId);
    }

    [Fact]
    public async Task Maps_401_to_an_expired_token()
    {
        using var handler = new StubHandler(HttpStatusCode.Unauthorized, "{}");
        using var client = new UsageApiClient(Endpoint, new HttpClient(handler));

        var error = await Assert.ThrowsAsync<UsageApiException>(() => client.FetchAsync(Credential));
        Assert.Equal(UsageErrorKind.AuthExpired, error.Kind);
    }

    [Fact]
    public async Task Maps_403_to_a_plan_problem()
    {
        using var handler = new StubHandler(HttpStatusCode.Forbidden, "{}");
        using var client = new UsageApiClient(Endpoint, new HttpClient(handler));

        var error = await Assert.ThrowsAsync<UsageApiException>(() => client.FetchAsync(Credential));
        Assert.Equal(UsageErrorKind.Forbidden, error.Kind);
    }

    [Fact]
    public async Task Maps_other_failures_to_http()
    {
        using var handler = new StubHandler(HttpStatusCode.InternalServerError, "boom");
        using var client = new UsageApiClient(Endpoint, new HttpClient(handler));

        var error = await Assert.ThrowsAsync<UsageApiException>(() => client.FetchAsync(Credential));
        Assert.Equal(UsageErrorKind.Http, error.Kind);
        Assert.Contains("500", error.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"usage":{"rolling":{"status":"ok","percent":1,"resetsAt":"2026-11-16T15:00:00Z"}}}""")]
    public async Task Maps_broken_payloads_to_parse(string body)
    {
        using var handler = new StubHandler(HttpStatusCode.OK, body);
        using var client = new UsageApiClient(Endpoint, new HttpClient(handler));

        var error = await Assert.ThrowsAsync<UsageApiException>(() => client.FetchAsync(Credential));
        Assert.Equal(UsageErrorKind.Parse, error.Kind);
    }

    [Fact]
    public async Task Rejects_a_missing_token()
    {
        using var client = new UsageApiClient(Endpoint, new HttpClient(new StubHandler(HttpStatusCode.OK, MinimalBody)));

        var error = await Assert.ThrowsAsync<UsageApiException>(
            () => client.FetchAsync(new AuthCredential("   ", CredentialKind.ApiKey)));
        Assert.Equal(UsageErrorKind.NotConfigured, error.Kind);
    }

    private const string MinimalBody = """
    {"usage":{"rolling":{"status":"ok","percent":1,"resetsAt":"2026-11-16T15:00:00Z"},
    "weekly":{"status":"ok","percent":2,"resetsAt":"2026-11-16T15:00:00Z"},
    "monthly":{"status":"ok","percent":3,"resetsAt":"2026-11-16T15:00:00Z"}}}
    """;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public string? Authorization { get; private set; }

        public string? OrganizationId { get; private set; }

        public string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization is { } header
                ? $"{header.Scheme} {header.Parameter}"
                : null;

            OrganizationId = request.Headers.TryGetValues("x-opencode-org-id", out var values)
                ? string.Join(",", values)
                : null;

            UserAgent = request.Headers.UserAgent.ToString();

            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };

            return Task.FromResult(response);
        }
    }
}
