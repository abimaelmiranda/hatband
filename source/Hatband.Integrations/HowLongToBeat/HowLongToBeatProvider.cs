using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hatband.Core.Abstractions.Integrations.HowLongToBeat;
using Hatband.Core.Models;
using Hatband.Integrations.HowLongToBeat.Models;

namespace Hatband.Integrations.HowLongToBeat;

/// <summary>
/// Coordinates searches against HowLongToBeat's undocumented search endpoint.
/// </summary>
public sealed class HowLongToBeatProvider : IHowLongToBeatProvider
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly HowLongToBeatEndpointDiscoverer endpointDiscoverer;
    private readonly HowLongToBeatSessionTokenProvider sessionTokenProvider;

    public HowLongToBeatProvider(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.httpClientFactory = httpClientFactory;
        endpointDiscoverer = new HowLongToBeatEndpointDiscoverer(httpClientFactory);
        sessionTokenProvider = new HowLongToBeatSessionTokenProvider(httpClientFactory, timeProvider);
    }

    public async Task<IReadOnlyList<HowLongToBeatGame>> SearchAsync(
        string gameName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);

        var currentSearchPath = await endpointDiscoverer.GetSearchPathAsync(false, cancellationToken);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await SearchAtPathAsync(gameName, currentSearchPath, cancellationToken);
            }
            catch (HttpRequestException exception) when (
                exception.StatusCode == HttpStatusCode.NotFound && attempt == 0)
            {
                sessionTokenProvider.Invalidate(currentSearchPath);
                currentSearchPath = await endpointDiscoverer.GetSearchPathAsync(true, cancellationToken);
                if (string.Equals(currentSearchPath, exception.Data["HltbApiPath"] as string, StringComparison.Ordinal))
                {
                    throw;
                }
            }
        }

        throw new InvalidOperationException("HowLongToBeat search could not resolve a working endpoint.");
    }

    private async Task<IReadOnlyList<HowLongToBeatGame>> SearchAtPathAsync(
        string gameName,
        string apiPath,
        CancellationToken cancellationToken)
    {
        var currentSession = await sessionTokenProvider.GetAsync(apiPath, cancellationToken);
        var searchUri = new Uri(HowLongToBeatProtocol.SiteBaseUri, apiPath);
        using var httpClient = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, searchUri);
        request.Headers.UserAgent.ParseAdd(HowLongToBeatProtocol.UserAgent);
        request.Headers.Referrer = HowLongToBeatProtocol.SiteBaseUri;
        request.Headers.Add("Origin", HowLongToBeatProtocol.SiteBaseUri.GetLeftPart(UriPartial.Authority));
        request.Headers.Add("x-auth-token", currentSession.Token);

        if (currentSession.HoneypotKey is not null && currentSession.HoneypotValue is not null)
        {
            request.Headers.Add("x-hp-key", currentSession.HoneypotKey);
            request.Headers.Add("x-hp-val", currentSession.HoneypotValue);
        }

        var payload = HowLongToBeatSearchPayload.Create(gameName, currentSession);
        request.Content = JsonContent.Create(
            payload,
            HowLongToBeatJsonSerializerContext.Default.HowLongToBeatSearchRequest);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        HowLongToBeatResponseStatus.EnsureSuccess(response, searchUri, apiPath);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var searchResponse = await JsonSerializer.DeserializeAsync(
            responseStream,
            HowLongToBeatJsonSerializerContext.Default.HowLongToBeatSearchResponse,
            cancellationToken);
        if (searchResponse is null)
        {
            throw new InvalidOperationException("The HowLongToBeat response body was empty.");
        }

        return searchResponse.ToGames();
    }
}
