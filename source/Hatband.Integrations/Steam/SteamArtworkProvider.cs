using System.Globalization;
using System.Text.Json;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Enums.Artwork;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;

namespace Hatband.Integrations.Steam;

/// <summary>
/// Resolves Steam library artwork through Steam's store item assets.
/// </summary>
public sealed class SteamArtworkProvider : IGameArtworkProvider
{
    private static readonly Uri StoreBrowseUri = new("https://api.steampowered.com/IStoreBrowseService/GetItems/v1/");
    private const string LegacyArtworkBaseUri = "https://steamcdn-a.akamaihd.net/steam/apps/";
    private const string AssetStoreBaseUri = "https://shared.akamai.steamstatic.com/store_item_assets/";

    private readonly HttpClient httpClient;

    public SteamArtworkProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
    }

    public GameSourceId? SourceId => GameSourceId.Steam;

    public string DisplayName => "Steam";

    public async Task<IReadOnlyList<GameArtworkImage>> GetDefaultArtworksAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        var sources = await GetArtworkSourcesAsync(game, languageTag, cancellationToken);
        var defaultSources = sources
            .GroupBy(source => source.Slot)
            .Select(group => group.First())
            .ToArray();
        return await DownloadArtworksAsync(defaultSources, cancellationToken);
    }

    public async Task<IReadOnlyList<GameArtworkImage>> GetArtworksAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        var sources = await GetArtworkSourcesAsync(game, languageTag, cancellationToken);
        return await DownloadArtworksAsync(sources, cancellationToken);
    }

    private async Task<IReadOnlyList<(string Url, GameArtworkSlot Slot)>> GetArtworkSourcesAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        if (game.SourceId != GameSourceId.Steam ||
            !uint.TryParse(game.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
        {
            return [];
        }

        var (steamLanguage, _) = SteamLanguage.Resolve(languageTag);
        cancellationToken.ThrowIfCancellationRequested();
        var storeAssets = await TryGetStoreAssetsAsync(appId, steamLanguage, cancellationToken);
        return CreateArtworkSources(appId, storeAssets);
    }

    private async Task<IReadOnlyList<GameArtworkImage>> DownloadArtworksAsync(
        IReadOnlyList<(string Url, GameArtworkSlot Slot)> sources,
        CancellationToken cancellationToken)
    {
        var images = new List<GameArtworkImage>(sources.Count);
        foreach (var (url, slot) in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var imageUri) ||
                (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            try
            {
                using var response = await httpClient.GetAsync(
                    imageUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (content.Length == 0)
                {
                    continue;
                }

                images.Add(new GameArtworkImage
                {
                    Slot = slot,
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"
                });
            }
            catch (HttpRequestException)
            {
                // Skip unavailable assets and keep loading the other artwork choices.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Skip timed out assets and keep loading the other artwork choices.
            }
        }

        return images;
    }

    private async Task<JsonElement?> TryGetStoreAssetsAsync(
        uint appId,
        string steamLanguage,
        CancellationToken cancellationToken)
    {
        var requestData = new Dictionary<string, object>
        {
            ["ids"] = new[]
            {
                new Dictionary<string, uint>
                {
                    ["appid"] = appId
                }
            },
            ["context"] = new Dictionary<string, string>
            {
                ["country_code"] = "US",
                ["language"] = steamLanguage
            },
            ["data_request"] = new Dictionary<string, bool>
            {
                ["include_assets"] = true
            }
        };
        var requestJson = JsonSerializer.Serialize(requestData);
        var requestUri = new Uri(
            $"{StoreBrowseUri}?input_json={Uri.EscapeDataString(requestJson)}");

        try
        {
            using var response = await httpClient.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("response", out var responseData) ||
                !responseData.TryGetProperty("store_items", out var storeItems) ||
                storeItems.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in storeItems.EnumerateArray())
            {
                if (!IsRequestedApp(item, appId))
                {
                    continue;
                }

                if (!item.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                return assets.Clone();
            }

            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsRequestedApp(JsonElement item, uint requestedAppId)
    {
        if (!item.TryGetProperty("appid", out var appIdValue))
        {
            return false;
        }

        if (appIdValue.ValueKind == JsonValueKind.Number && appIdValue.TryGetUInt32(out var numericAppId))
        {
            return numericAppId == requestedAppId;
        }

        if (appIdValue.ValueKind == JsonValueKind.String &&
            uint.TryParse(appIdValue.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out numericAppId))
        {
            return numericAppId == requestedAppId;
        }

        return false;
    }

    private static IReadOnlyList<(string Url, GameArtworkSlot Slot)> CreateArtworkSources(
        uint appId,
        JsonElement? storeAssets)
    {
        var appIdText = appId.ToString(CultureInfo.InvariantCulture);
        var sources = new List<(string Url, GameArtworkSlot Slot)>();

        if (storeAssets is JsonElement assets)
        {
            var assetUrlFormat = ReadAssetString(assets, "asset_url_format");
            AddResolvedAsset(sources, assets, assetUrlFormat, "library_capsule_2x", GameArtworkSlot.Cover);
            AddResolvedAsset(sources, assets, assetUrlFormat, "library_capsule", GameArtworkSlot.Cover);
            AddResolvedAsset(sources, assets, assetUrlFormat, "hero_capsule", GameArtworkSlot.Cover);
            AddResolvedAsset(sources, assets, assetUrlFormat, "hero_capsule_2x", GameArtworkSlot.Cover);
            AddResolvedAsset(sources, assets, assetUrlFormat, "main_capsule", GameArtworkSlot.Cover);
            AddResolvedAsset(sources, assets, assetUrlFormat, "main_capsule_2x", GameArtworkSlot.Cover);
            AddResolvedAsset(sources, assets, assetUrlFormat, "small_capsule", GameArtworkSlot.Cover);
            AddResolvedAsset(sources, assets, assetUrlFormat, "small_capsule_2x", GameArtworkSlot.Cover);

            AddResolvedAsset(sources, assets, assetUrlFormat, "library_hero", GameArtworkSlot.Background);
            AddResolvedAsset(sources, assets, assetUrlFormat, "library_hero_2x", GameArtworkSlot.Background);
            AddResolvedAsset(sources, assets, assetUrlFormat, "raw", GameArtworkSlot.Background);
            AddResolvedAsset(sources, assets, assetUrlFormat, "raw_2x", GameArtworkSlot.Background);
            AddResolvedAsset(sources, assets, assetUrlFormat, "raw_page_background", GameArtworkSlot.Background);
            AddResolvedAsset(sources, assets, assetUrlFormat, "page_background", GameArtworkSlot.Background);
            AddResolvedAsset(sources, assets, assetUrlFormat, "library_header", GameArtworkSlot.Background);
            AddResolvedAsset(sources, assets, assetUrlFormat, "library_header_2x", GameArtworkSlot.Background);
            AddResolvedAsset(sources, assets, assetUrlFormat, "header", GameArtworkSlot.Background);
            AddResolvedAsset(sources, assets, assetUrlFormat, "header_2x", GameArtworkSlot.Background);
        }

        AddSource(sources, $"{LegacyArtworkBaseUri}{appIdText}/library_600x900_2x.jpg", GameArtworkSlot.Cover);
        AddSource(sources, $"{LegacyArtworkBaseUri}{appIdText}/library_600x900.jpg", GameArtworkSlot.Cover);
        AddSource(sources, $"{LegacyArtworkBaseUri}{appIdText}/hero_capsule.jpg", GameArtworkSlot.Cover);
        AddSource(sources, $"{LegacyArtworkBaseUri}{appIdText}/capsule_616x353.jpg", GameArtworkSlot.Cover);
        AddSource(sources, $"{LegacyArtworkBaseUri}{appIdText}/library_hero.jpg", GameArtworkSlot.Background);
        AddSource(sources, $"{LegacyArtworkBaseUri}{appIdText}/background.jpg", GameArtworkSlot.Background);
        AddSource(sources, $"{LegacyArtworkBaseUri}{appIdText}/header.jpg", GameArtworkSlot.Background);

        return sources;
    }

    private static void AddResolvedAsset(
        ICollection<(string Url, GameArtworkSlot Slot)> sources,
        JsonElement assets,
        string? assetUrlFormat,
        string assetName,
        GameArtworkSlot slot)
    {
        if (string.IsNullOrWhiteSpace(assetUrlFormat))
        {
            return;
        }

        var assetPath = ReadAssetString(assets, assetName);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        var assetUrl = assetUrlFormat.Replace("${FILENAME}", assetPath, StringComparison.Ordinal);
        if (!Uri.TryCreate(assetUrl, UriKind.Absolute, out var absoluteUri))
        {
            var relativeAssetPath = assetUrl.TrimStart('/');
            if (!Uri.TryCreate(new Uri(AssetStoreBaseUri), relativeAssetPath, out absoluteUri))
            {
                return;
            }
        }

        AddSource(sources, absoluteUri.AbsoluteUri, slot);
    }

    private static void AddSource(
        ICollection<(string Url, GameArtworkSlot Slot)> sources,
        string url,
        GameArtworkSlot slot)
    {
        if (sources.Any(source => string.Equals(source.Url, url, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        sources.Add((url, slot));
    }

    private static string? ReadAssetString(JsonElement assets, string propertyName)
    {
        if (!assets.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

}
