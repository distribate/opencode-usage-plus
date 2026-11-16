using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OpenCodeUsagePlus.Core.Models;

namespace OpenCodeUsagePlus.Core.Services;

/// <summary>
/// Thin client for the (undocumented) OpenCode Go usage endpoint. Isolated on purpose: if the schema
/// changes, only this file and its tests need to move.
/// </summary>
public sealed class UsageApiClient : IDisposable
{
    public const string ProductName = "OpenCodeUsagePlus";

    public static readonly Uri DefaultEndpoint = new("https://opencode.ai/zen/go/v1/usage");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Version AppVersion =
        typeof(UsageApiClient).Assembly.GetName().Version ?? new Version(0, 1, 0);

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _endpoint;

    public UsageApiClient(Uri? endpoint = null, HttpClient? httpClient = null)
    {
        _endpoint = endpoint ?? DefaultEndpoint;

        if (httpClient is not null)
        {
            _http = httpClient;
            _ownsHttp = false;
            return;
        }

        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(10),
        };

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _ownsHttp = true;
    }

    public async Task<UsageStats> FetchAsync(AuthCredential credential, CancellationToken cancellationToken = default)
    {
        if (credential is null || string.IsNullOrWhiteSpace(credential.Token))
        {
            throw new UsageApiException(UsageErrorKind.NotConfigured);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", $"{ProductName}/{AppVersion} (Windows)");

        if (!string.IsNullOrWhiteSpace(credential.OrganizationId))
        {
            request.Headers.TryAddWithoutValidation("x-opencode-org-id", credential.OrganizationId);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UsageApiException(UsageErrorKind.Network, "The usage request timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new UsageApiException(UsageErrorKind.Network, ex.Message, ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new UsageApiException(UsageErrorKind.AuthExpired);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new UsageApiException(UsageErrorKind.Forbidden);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await SafeReadAsync(response, cancellationToken).ConfigureAwait(false);
                var detail = string.IsNullOrWhiteSpace(body) ? string.Empty : $": {Truncate(body, 160)}";
                throw new UsageApiException(UsageErrorKind.Http, $"HTTP {(int)response.StatusCode}{detail}");
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var payload = await JsonSerializer
                    .DeserializeAsync<UsageResponseDto>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);

                var usage = payload?.Usage;
                if (usage?.Rolling is null || usage.Weekly is null || usage.Monthly is null)
                {
                    throw new UsageApiException(UsageErrorKind.Parse, "The usage response is incomplete.");
                }

                return new UsageStats(Map(usage.Rolling), Map(usage.Weekly), Map(usage.Monthly));
            }
            catch (JsonException ex)
            {
                throw new UsageApiException(UsageErrorKind.Parse, ex.Message, ex);
            }
        }
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private static UsageItem Map(UsageMeterDto dto) => new(dto.Status ?? "ok", dto.Percent, dto.ResetsAt);

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length] + "…";

    internal sealed record UsageMeterDto(string? Status, double Percent, DateTimeOffset ResetsAt);

    internal sealed record UsageStatsDto(UsageMeterDto Rolling, UsageMeterDto Weekly, UsageMeterDto Monthly);

    internal sealed record UsageResponseDto(UsageStatsDto Usage);
}
