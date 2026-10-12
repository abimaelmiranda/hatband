using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Hatband.Core.Enums.Artwork;
using Hatband.Plugins.SteamGridDB.Localization;
using Hatband.Plugins.SteamGridDB.Authentication;

namespace Hatband.Plugins.SteamGridDB.Api;

public sealed class SteamGridDBApiClient
{
    private static readonly Uri ApiRoot = new("https://www.steamgriddb.com/api/v2/");
    private readonly IHttpClientFactory httpClientFactory;

    public SteamGridDBApiClient(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<int?> FindGameIdAsync(string apiKey, string? steamAppId, string title, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("Hatband.Plugin.SteamGridDB.Api");
        if (steamAppId is not null)
        {
            try
            {
                using var document = await SendJsonAsync(client,
                    new Uri(ApiRoot, $"games/steam/{Uri.EscapeDataString(steamAppId)}"), apiKey, cancellationToken);
                return SteamGridDBGameParser.ReadGameId(document.RootElement);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        try
        {
            using var search = await SendJsonAsync(client,
                new Uri(ApiRoot, $"search/autocomplete/{Uri.EscapeDataString(title)}"), apiKey, cancellationToken);
            return SteamGridDBGameParser.FindUniqueExactGameId(search.RootElement, title);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SteamGridDBAsset>> GetAssetsAsync(
        string apiKey,
        int gameId,
        IReadOnlySet<GameArtworkSlot> slots,
        int limit,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("Hatband.Plugin.SteamGridDB.Api");
        var results = new List<SteamGridDBAsset>();
        foreach (var slot in slots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var endpoint = slot switch
            {
                GameArtworkSlot.Cover => "grids",
                GameArtworkSlot.Background => "heroes",
                GameArtworkSlot.Icon => "icons",
                GameArtworkSlot.Logo => "logos",
                _ => throw new ArgumentOutOfRangeException(nameof(slot))
            };
            var mimeFilter = slot is GameArtworkSlot.Icon or GameArtworkSlot.Logo ? "image/png" : "image/png,image/jpeg";
            var dimensionFilter = slot == GameArtworkSlot.Cover ? "&dimensions=600x900" : string.Empty;
            var uri = new Uri(ApiRoot,
                $"{endpoint}/game/{gameId.ToString(CultureInfo.InvariantCulture)}?limit={limit.ToString(CultureInfo.InvariantCulture)}&types=static&mimes={Uri.EscapeDataString(mimeFilter)}&nsfw=false&epilepsy=false&humor=false{dimensionFilter}");
            try
            {
                using var document = await SendJsonAsync(client, uri, apiKey, cancellationToken);
                results.AddRange(SteamGridDBAssetParser.ReadAssets(document.RootElement, slot));
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }
        }

        return results;
    }

    private static async Task<JsonDocument> SendJsonAsync(
        HttpClient client,
        Uri uri,
        string apiKey,
        CancellationToken cancellationToken)
    {
        SteamGridDBCredentialService.ValidateApiKey(apiKey);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new HttpRequestException(SteamGridDBResources.ApiRedirected);
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new HttpRequestException(SteamGridDBResources.ApiKeyRejected, null, response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
        var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (document.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
        {
            document.Dispose();
            throw new HttpRequestException("SteamGridDB API returned an unsuccessful response.");
        }

        return document;
    }
}
