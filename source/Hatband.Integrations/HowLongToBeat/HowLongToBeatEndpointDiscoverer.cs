using System.Text.RegularExpressions;

namespace Hatband.Integrations.HowLongToBeat;

internal sealed partial class HowLongToBeatEndpointDiscoverer
{
    [GeneratedRegex("<script\\b[^>]*?src=[\\\"']([^\\\"']+\\.js(?:\\?[^\\\"']*)?)[\\\"'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptSourcePattern();

    [GeneratedRegex(
        "fetch\\s*\\(\\s*[\\\"']\\/api\\/([a-zA-Z0-9_\\/]+)[^\\\"']*[\\\"']\\s*,\\s*\\{[^}]*method:\\s*[\\\"']POST[\\\"'][^}]*\\}",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SearchEndpointPattern();

    private readonly IHttpClientFactory httpClientFactory;
    private readonly SemaphoreSlim endpointLock = new(1, 1);
    private string? searchPath;

    public HowLongToBeatEndpointDiscoverer(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetSearchPathAsync(
        bool forceRediscovery,
        CancellationToken cancellationToken)
    {
        if (!forceRediscovery && searchPath is not null)
        {
            return searchPath;
        }

        await endpointLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRediscovery && searchPath is not null)
            {
                return searchPath;
            }

            searchPath = await DiscoverSearchPathAsync(cancellationToken);
            return searchPath;
        }
        finally
        {
            endpointLock.Release();
        }
    }

    private async Task<string> DiscoverSearchPathAsync(CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient();
        using var homepageRequest = new HttpRequestMessage(HttpMethod.Get, HowLongToBeatProtocol.SiteBaseUri);
        homepageRequest.Headers.UserAgent.ParseAdd(HowLongToBeatProtocol.UserAgent);
        homepageRequest.Headers.Referrer = HowLongToBeatProtocol.SiteBaseUri;
        using var homepageResponse = await httpClient.SendAsync(homepageRequest, cancellationToken);
        HowLongToBeatResponseStatus.EnsureSuccess(homepageResponse, HowLongToBeatProtocol.SiteBaseUri);
        var homepage = await homepageResponse.Content.ReadAsStringAsync(cancellationToken);

        foreach (Match scriptMatch in ScriptSourcePattern().Matches(homepage))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scriptUri = new Uri(HowLongToBeatProtocol.SiteBaseUri, scriptMatch.Groups[1].Value);
            using var scriptRequest = new HttpRequestMessage(HttpMethod.Get, scriptUri);
            scriptRequest.Headers.UserAgent.ParseAdd(HowLongToBeatProtocol.UserAgent);
            scriptRequest.Headers.Referrer = HowLongToBeatProtocol.SiteBaseUri;

            using var scriptResponse = await httpClient.SendAsync(scriptRequest, cancellationToken);
            if (!scriptResponse.IsSuccessStatusCode)
            {
                continue;
            }

            var script = await scriptResponse.Content.ReadAsStringAsync(cancellationToken);
            foreach (Match endpointMatch in SearchEndpointPattern().Matches(script))
            {
                var endpointSuffix = endpointMatch.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(endpointSuffix) ||
                    string.Equals(endpointSuffix, "find", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return $"/api/{endpointSuffix}";
            }
        }

        return HowLongToBeatProtocol.SearchPathFallback;
    }
}
