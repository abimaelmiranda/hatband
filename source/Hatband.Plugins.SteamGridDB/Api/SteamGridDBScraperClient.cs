using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hatband.Core.Enums.Artwork;

namespace Hatband.Plugins.SteamGridDB.Api;

public sealed partial class SteamGridDBScraperClient
{
    private static readonly Uri SiteRoot = new("https://www.steamgriddb.com/");
    private readonly IHttpClientFactory httpClientFactory;

    public SteamGridDBScraperClient(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<int?> FindGameIdAsync(string? steamAppId, string title, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("Hatband.Plugin.SteamGridDB.Scraper");
        if (steamAppId is not null)
        {
            var steamUri = new Uri(SiteRoot, $"steam/{Uri.EscapeDataString(steamAppId)}");
            using var response = await client.GetAsync(steamUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            EnsureScraperResponse(response);
            var finalUri = response.RequestMessage?.RequestUri;
            if (finalUri is null)
            {
                return null;
            }

            var match = GameRouteRegex().Match(finalUri.AbsolutePath);
            return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? id
                : null;
        }

        var searchUri = new Uri(SiteRoot, $"api/public/search/autocomplete?term={Uri.EscapeDataString(title)}");
        using var searchResponse = await client.GetAsync(searchUri, cancellationToken);
        if (searchResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureScraperResponse(searchResponse);
        using var document = await JsonDocument.ParseAsync(
            await searchResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return SteamGridDBGameParser.FindUniqueExactGameId(document.RootElement, title);
    }

    public async Task<IReadOnlyList<SteamGridDBAsset>> GetAssetsAsync(
        int gameId,
        IReadOnlySet<GameArtworkSlot> slots,
        int limit,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("Hatband.Plugin.SteamGridDB.Scraper");
        var results = new List<SteamGridDBAsset>();
        foreach (var slot in slots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(SiteRoot, "api/public/search/assets"));
            request.Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["asset_type"] = ToAssetType(slot),
                ["game_id"] = new[] { gameId },
                ["page"] = 0,
                ["limit"] = limit,
                ["styles"] = new[] { "all" },
                ["languages"] = new[] { "all" },
                ["dimensions"] = slot == GameArtworkSlot.Cover ? new[] { "600x900" } : new[] { "all" },
                ["mime"] = slot is GameArtworkSlot.Icon or GameArtworkSlot.Logo
                    ? new[] { "image/png" }
                    : new[] { "image/png", "image/jpeg" },
                ["order"] = "score_desc",
                ["static"] = true,
                ["animated"] = false,
                ["nsfw"] = false,
                ["epilepsy"] = false,
                ["humor"] = false,
                ["untagged"] = true
            }), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            EnsureScraperResponse(response);
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            results.AddRange(SteamGridDBAssetParser.ReadAssets(document.RootElement, slot));
        }

        return results;
    }

    private static void EnsureScraperResponse(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new SteamGridDBScraperForbiddenException();
        }

        response.EnsureSuccessStatusCode();
    }

    private static string ToAssetType(GameArtworkSlot slot) => slot switch
    {
        GameArtworkSlot.Cover => "grid",
        GameArtworkSlot.Background => "hero",
        GameArtworkSlot.Icon => "icon",
        GameArtworkSlot.Logo => "logo",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    [GeneratedRegex("/game/(\\d+)(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GameRouteRegex();
}

internal sealed class SteamGridDBScraperForbiddenException : HttpRequestException
{
    public SteamGridDBScraperForbiddenException()
        : base("SteamGridDB public scraper request was forbidden.", null, HttpStatusCode.Forbidden)
    {
    }
}
