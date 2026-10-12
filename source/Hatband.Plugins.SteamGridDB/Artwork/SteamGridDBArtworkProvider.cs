using System.Globalization;
using System.Net;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Services;
using Hatband.Core.Abstractions.Settings;
using Hatband.Core.Enums.Artwork;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Hatband.Plugins.SteamGridDB.Api;
using Hatband.Plugins.SteamGridDB.Authentication;
using Hatband.Plugins.SteamGridDB.Settings;
using Hatband.Plugins.SteamGridDB.Localization;
using Microsoft.Extensions.Logging;

namespace Hatband.Plugins.SteamGridDB.Artwork;

/// <summary>Finds SteamGridDB artwork through its public scraper pages or authenticated API.</summary>
public sealed class SteamGridDBArtworkProvider : IGameArtworkProvider
{
    private static readonly IReadOnlySet<GameArtworkSlot> AllSlots = new HashSet<GameArtworkSlot>(Enum.GetValues<GameArtworkSlot>());
    private static readonly SemaphoreSlim[] CacheGates = Enumerable.Range(0, 32)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ICacheService cacheService;
    private readonly ISettingsApi settingsApi;
    private readonly SteamGridDBCredentialService credentials;
    private readonly SteamGridDBScraperClient scraperClient;
    private readonly SteamGridDBApiClient apiClient;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SteamGridDBArtworkProvider> logger;

    public SteamGridDBArtworkProvider(
        IHttpClientFactory httpClientFactory,
        ICacheService cacheService,
        ISettingsApi settingsApi,
        SteamGridDBCredentialService credentials,
        SteamGridDBScraperClient scraperClient,
        SteamGridDBApiClient apiClient,
        TimeProvider timeProvider,
        ILogger<SteamGridDBArtworkProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(settingsApi);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(scraperClient);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        this.httpClientFactory = httpClientFactory;
        this.cacheService = cacheService;
        this.settingsApi = settingsApi;
        this.credentials = credentials;
        this.scraperClient = scraperClient;
        this.apiClient = apiClient;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public string? SourceId => null;

    public string DisplayName => SteamGridDBResources.PluginName;

    public IReadOnlySet<GameArtworkSlot> SupportedSlots => AllSlots;

    public Task<IReadOnlyList<GameArtworkImage>> GetDefaultArtworksAsync(
        Game game,
        IReadOnlySet<GameArtworkSlot> requestedSlots,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        return GetArtworksCoreAsync(game, requestedSlots, languageTag, 1, cancellationToken);
    }

    public Task<IReadOnlyList<GameArtworkImage>> GetArtworksAsync(
        Game game,
        IReadOnlySet<GameArtworkSlot> requestedSlots,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        return GetArtworksCoreAsync(game, requestedSlots, languageTag, 10, cancellationToken);
    }

    private async Task<IReadOnlyList<GameArtworkImage>> GetArtworksCoreAsync(
        Game game,
        IReadOnlySet<GameArtworkSlot> requestedSlots,
        string languageTag,
        int resultLimitPerSlot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(game.Name);
        ArgumentNullException.ThrowIfNull(requestedSlots);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        cancellationToken.ThrowIfCancellationRequested();

        if (requestedSlots.Any(slot => !AllSlots.Contains(slot)))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedSlots), "A requested artwork slot is not supported.");
        }

        var slots = requestedSlots.ToHashSet();
        if (slots.Count == 0)
        {
            return [];
        }

        var settings = await settingsApi.GetSectionAsync<SteamGridDBSettings>(cancellationToken);
        var mode = settings.Mode;
        if (!Enum.IsDefined(mode))
        {
            throw new InvalidDataException(SteamGridDBResources.InvalidMode);
        }

        var apiKey = mode == SteamGridDBMode.ApiKey
            ? await credentials.GetApiKeyAsync(cancellationToken)
            : null;
        var effectiveResultLimitPerSlot = mode == SteamGridDBMode.ApiKey
            ? resultLimitPerSlot
            : 1;
        var steamAppId = TryGetSteamAppId(game);
        var gameIdentity = steamAppId is not null
            ? $"steam:{steamAppId}"
            : $"title:{SteamGridDBGameParser.NormalizeTitle(game.Name)}";

        IReadOnlyList<SteamGridDBAsset> assets;
        try
        {
            assets = await GetCachedAssetsAsync(mode, apiKey, gameIdentity, steamAppId, game.Name, slots, effectiveResultLimitPerSlot, cancellationToken);
        }
        catch (SteamGridDBScraperForbiddenException)
        {
            logger.LogWarning("SteamGridDB public scraper was forbidden; no scraper artwork was returned.");
            return [];
        }

        return await DownloadAssetsAsync(assets, cancellationToken);
    }

    private async Task<IReadOnlyList<SteamGridDBAsset>> GetCachedAssetsAsync(
        SteamGridDBMode mode,
        string? apiKey,
        string gameIdentity,
        string? steamAppId,
        string title,
        IReadOnlySet<GameArtworkSlot> slots,
        int resultLimitPerSlot,
        CancellationToken cancellationToken)
    {
        var modeKey = mode == SteamGridDBMode.ApiKey ? "api" : "scraper";
        var slotKey = string.Join(',', slots.Order().Select(slot => slot.ToString().ToLowerInvariant()));
        var cacheKey = $"steamgriddb:{modeKey}:{gameIdentity}:{slotKey}:{resultLimitPerSlot.ToString(CultureInfo.InvariantCulture)}";
        var gateIndex = (int)((uint)StringComparer.Ordinal.GetHashCode(cacheKey) % (uint)CacheGates.Length);
        var gate = CacheGates[gateIndex];
        await gate.WaitAsync(cancellationToken);
        try
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var negativeCacheKey = $"{cacheKey}:negative:{today:yyyyMMdd}";
            return await cacheService.GetOrCreateAsync<IReadOnlyList<SteamGridDBAsset>>(
                negativeCacheKey,
                today,
                async () =>
                {
                    var cachedAssets = await cacheService.GetOrCreateAsync<IReadOnlyList<SteamGridDBAsset>?>(
                        cacheKey,
                        today.AddDays(6),
                        async () =>
                        {
                            var result = await FindAssetsAsync(mode, apiKey, steamAppId, title, slots, resultLimitPerSlot, cancellationToken);
                            return result.Count == 0 ? null : result;
                        },
                        cancellationToken);
                    return cachedAssets ?? [];
                },
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<SteamGridDBAsset>> FindAssetsAsync(
        SteamGridDBMode mode,
        string? apiKey,
        string? steamAppId,
        string title,
        IReadOnlySet<GameArtworkSlot> slots,
        int resultLimitPerSlot,
        CancellationToken cancellationToken)
    {
        var gameId = mode == SteamGridDBMode.ApiKey
            ? await apiClient.FindGameIdAsync(RequireApiKey(apiKey), steamAppId, title, cancellationToken)
            : await scraperClient.FindGameIdAsync(steamAppId, title, cancellationToken);
        if (gameId is null)
        {
            return [];
        }

        return mode == SteamGridDBMode.ApiKey
            ? await apiClient.GetAssetsAsync(RequireApiKey(apiKey), gameId.Value, slots, resultLimitPerSlot, cancellationToken)
            : await scraperClient.GetAssetsAsync(gameId.Value, slots, resultLimitPerSlot, cancellationToken);
    }

    private async Task<IReadOnlyList<GameArtworkImage>> DownloadAssetsAsync(
        IReadOnlyList<SteamGridDBAsset> assets,
        CancellationToken cancellationToken)
    {
        var images = new List<GameArtworkImage>(assets.Count);
        using var client = httpClientFactory.CreateClient("Hatband.Plugin.SteamGridDB.Scraper");
        foreach (var asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        logger.LogWarning("SteamGridDB artwork download was forbidden; the image was skipped.");
                    }

                    continue;
                }

                var responseContentType = response.Content.Headers.ContentType?.MediaType;
                if (responseContentType is not null && !SteamGridDBAssetParser.IsAcceptedType(asset.Slot, responseContentType))
                {
                    continue;
                }

                var actualContentType = responseContentType ?? asset.ContentType;
                if (!SteamGridDBAssetParser.IsAcceptedType(asset.Slot, actualContentType))
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
                    Slot = asset.Slot,
                    Content = content,
                    ContentType = actualContentType.ToLowerInvariant()
                });
            }
            catch (HttpRequestException)
            {
                // An unavailable CDN item should not prevent other artwork choices from loading.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A CDN timeout should not prevent other artwork choices from loading.
            }
        }

        return images;
    }

    private static string RequireApiKey(string? apiKey) =>
        apiKey ?? throw new InvalidOperationException("A SteamGridDB API key is required.");

    private static string? TryGetSteamAppId(Game game)
    {
        var storeId = game.SourceId == GameSourceIds.Steam
            ? game.SourceGameId
            : game.Metadata.StoreSourceId == GameSourceIds.Steam
                ? game.Metadata.StoreGameId
                : null;
        return uint.TryParse(storeId, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? storeId
            : null;
    }
}
