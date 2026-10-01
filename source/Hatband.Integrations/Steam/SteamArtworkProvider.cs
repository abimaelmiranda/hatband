using System.Globalization;
using System.Text.Json;
using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Integrations.Steam;

/// <summary>
/// Resolves Steam library artwork through Steam's store item assets.
/// </summary>
public sealed class SteamArtworkProvider : IGameArtworkProvider, IGameArtworkSearchProvider
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

    public GameSourceId SourceId => GameSourceId.Steam;

    public string ProviderId => "steam";

    public string DisplayName => "Steam";

    public bool CanSearch(GameSourceLookupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.SourceId == GameSourceId.Steam &&
               uint.TryParse(request.SourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    public async Task<GameArtworkSearchResponse> SearchArtworkAsync(
        GameSourceLookupRequest request,
        string preferredLanguageTag,
        string region,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguageTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        if (!CanSearch(request))
        {
            return new GameArtworkSearchResponse
            {
                ErrorMessage = "A Steam app ID is required to search Steam artwork."
            };
        }

        if (string.IsNullOrWhiteSpace(request.SourceGameId))
        {
            throw new InvalidOperationException("A searchable Steam request must contain a Steam app ID.");
        }

        var sources = await GetArtworkSourcesAsync(
            request.SourceGameId,
            preferredLanguageTag,
            cancellationToken);
        return sources is null
            ? new GameArtworkSearchResponse { ErrorMessage = "Steam did not return artwork for this game." }
            : new GameArtworkSearchResponse { Sources = sources };
    }

    public async Task<GameArtworkSources?> GetArtworkSourcesAsync(
        string sourceGameId,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceGameId);
        if (!uint.TryParse(sourceGameId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
        {
            throw new ArgumentException("Steam game IDs must be numeric app IDs.", nameof(sourceGameId));
        }

        var (steamLanguage, _) = SteamLanguage.Resolve(languageTag);
        cancellationToken.ThrowIfCancellationRequested();
        var storeAssets = await TryGetStoreAssetsAsync(appId, steamLanguage, cancellationToken);
        return CreateArtworkSources(appId, storeAssets);
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

    private static GameArtworkSources CreateArtworkSources(uint appId, JsonElement? storeAssets)
    {
        var appIdText = appId.ToString(CultureInfo.InvariantCulture);
        var coverImageUrls = new List<string>();
        var backgroundImageUrls = new List<string>();
        var coverImageCandidates = new List<GameArtworkCandidate>();
        var backgroundImageCandidates = new List<GameArtworkCandidate>();

        if (storeAssets is JsonElement assets)
        {
            var assetUrlFormat = ReadAssetString(assets, "asset_url_format");
            AddResolvedAsset(coverImageUrls, coverImageCandidates, assets, assetUrlFormat, "library_capsule_2x", "Library Capsule 2×");
            AddResolvedAsset(coverImageUrls, coverImageCandidates, assets, assetUrlFormat, "library_capsule", "Library Capsule");
            AddResolvedAsset(coverImageUrls, coverImageCandidates, assets, assetUrlFormat, "hero_capsule", "Vertical Capsule");
            AddResolvedAsset(coverImageUrls, coverImageCandidates, assets, assetUrlFormat, "hero_capsule_2x", "Vertical Capsule 2×");
            AddResolvedAsset(coverImageUrls, coverImageCandidates, assets, assetUrlFormat, "main_capsule", "Main Capsule");
            AddResolvedAsset(coverImageUrls, coverImageCandidates, assets, assetUrlFormat, "main_capsule_2x", "Main Capsule 2×");
            AddResolvedAsset(coverImageUrls, coverImageCandidates, assets, assetUrlFormat, "small_capsule", "Small Capsule");
            AddResolvedAsset(coverImageUrls, coverImageCandidates, assets, assetUrlFormat, "small_capsule_2x", "Small Capsule 2×");

            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "library_hero", "Library Hero");
            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "library_hero_2x", "Library Hero 2×");
            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "raw", "Raw Background");
            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "raw_2x", "Raw Background 2×");
            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "raw_page_background", "Raw Page Background");
            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "page_background", "Page Background");
            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "library_header", "Library Header");
            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "library_header_2x", "Library Header 2×");
            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "header", "Header");
            AddResolvedAsset(backgroundImageUrls, backgroundImageCandidates, assets, assetUrlFormat, "header_2x", "Header 2×");
        }

        AddFallbackCandidate(coverImageUrls, coverImageCandidates, $"{LegacyArtworkBaseUri}{appIdText}/library_600x900_2x.jpg", "Library Capsule 2× · Legacy");
        AddFallbackCandidate(coverImageUrls, coverImageCandidates, $"{LegacyArtworkBaseUri}{appIdText}/library_600x900.jpg", "Library Capsule · Legacy");
        AddFallbackCandidate(coverImageUrls, coverImageCandidates, $"{LegacyArtworkBaseUri}{appIdText}/hero_capsule.jpg", "Vertical Capsule · Legacy");
        AddFallbackCandidate(coverImageUrls, coverImageCandidates, $"{LegacyArtworkBaseUri}{appIdText}/capsule_616x353.jpg", "Main Capsule · Legacy");
        AddFallbackCandidate(backgroundImageUrls, backgroundImageCandidates, $"{LegacyArtworkBaseUri}{appIdText}/library_hero.jpg", "Library Hero · Legacy");
        AddFallbackCandidate(backgroundImageUrls, backgroundImageCandidates, $"{LegacyArtworkBaseUri}{appIdText}/background.jpg", "Raw Background · Legacy");
        AddFallbackCandidate(backgroundImageUrls, backgroundImageCandidates, $"{LegacyArtworkBaseUri}{appIdText}/header.jpg", "Header · Legacy");

        return new GameArtworkSources
        {
            CoverImageUrls = coverImageUrls,
            BackgroundImageUrls = backgroundImageUrls,
            CoverImageCandidates = coverImageCandidates,
            BackgroundImageCandidates = backgroundImageCandidates
        };
    }

    private static void AddResolvedAsset(
        ICollection<string> urls,
        ICollection<GameArtworkCandidate> candidates,
        JsonElement assets,
        string? assetUrlFormat,
        string assetName,
        string displayName)
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

        AddCandidate(urls, candidates, absoluteUri.AbsoluteUri, displayName);
    }

    private static void AddCandidate(
        ICollection<string> urls,
        ICollection<GameArtworkCandidate> candidates,
        string url,
        string caption)
    {
        if (urls.Contains(url, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        urls.Add(url);
        candidates.Add(new GameArtworkCandidate
        {
            Url = url,
            Caption = caption
        });
    }

    private static void AddFallbackCandidate(
        ICollection<string> urls,
        ICollection<GameArtworkCandidate> candidates,
        string url,
        string caption)
    {
        if (candidates.Any(candidate => string.Equals(candidate.Caption, caption, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        AddCandidate(urls, candidates, url, caption);
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
