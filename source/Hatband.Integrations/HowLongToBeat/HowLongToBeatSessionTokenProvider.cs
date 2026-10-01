using System.Text.Json;
using Hatband.Integrations.HowLongToBeat.Models;

namespace Hatband.Integrations.HowLongToBeat;

internal sealed class HowLongToBeatSessionTokenProvider
{
    private static readonly TimeSpan SessionTokenLifetime = TimeSpan.FromMinutes(10);

    private readonly HttpClient httpClient;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private HowLongToBeatSearchSessionToken? sessionToken;

    public HowLongToBeatSessionTokenProvider(HttpClient httpClient, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.httpClient = httpClient;
        this.timeProvider = timeProvider;
    }

    public async Task<HowLongToBeatSearchSessionToken> GetAsync(
        string apiPath,
        CancellationToken cancellationToken)
    {
        if (IsFreshFor(apiPath))
        {
            return GetCurrentToken();
        }

        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (IsFreshFor(apiPath))
            {
                return GetCurrentToken();
            }

            return await RequestTokenAsync(apiPath, cancellationToken);
        }
        finally
        {
            tokenLock.Release();
        }
    }

    public void Invalidate(string apiPath)
    {
        if (sessionToken is not null && string.Equals(sessionToken.ApiPath, apiPath, StringComparison.Ordinal))
        {
            sessionToken = null;
        }
    }

    private async Task<HowLongToBeatSearchSessionToken> RequestTokenAsync(
        string apiPath,
        CancellationToken cancellationToken)
    {
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var tokenUri = new Uri(HowLongToBeatProtocol.SiteBaseUri, $"{apiPath}/init?t={timestamp}");
        using var request = new HttpRequestMessage(HttpMethod.Get, tokenUri);
        request.Headers.UserAgent.ParseAdd(HowLongToBeatProtocol.UserAgent);
        request.Headers.Referrer = HowLongToBeatProtocol.SiteBaseUri;

        using var response = await httpClient.SendAsync(request, cancellationToken);
        HowLongToBeatResponseStatus.EnsureSuccess(response, tokenUri, apiPath);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var sessionResponse = await JsonSerializer.DeserializeAsync(
            responseStream,
            HowLongToBeatJsonSerializerContext.Default.HowLongToBeatSessionResponse,
            cancellationToken);
        if (sessionResponse is null || string.IsNullOrWhiteSpace(sessionResponse.Token))
        {
            throw new InvalidOperationException("The HowLongToBeat response is missing 'token'.");
        }

        var createdSessionToken = new HowLongToBeatSearchSessionToken(
            apiPath,
            sessionResponse.Token,
            sessionResponse.HoneypotKey,
            sessionResponse.HoneypotValue,
            timeProvider.GetUtcNow());
        sessionToken = createdSessionToken;
        return createdSessionToken;
    }

    private bool IsFreshFor(string apiPath)
    {
        return sessionToken is not null &&
               string.Equals(sessionToken.ApiPath, apiPath, StringComparison.Ordinal) &&
               sessionToken.CreatedAtUtc + SessionTokenLifetime > timeProvider.GetUtcNow();
    }

    private HowLongToBeatSearchSessionToken GetCurrentToken()
    {
        return sessionToken ?? throw new InvalidOperationException("The HowLongToBeat session token is unavailable.");
    }
}
