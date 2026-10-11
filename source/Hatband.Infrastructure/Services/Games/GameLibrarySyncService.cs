using Hatband.Core.Services;
using Microsoft.Extensions.Logging;

namespace Hatband.Infrastructure.Services.Games;

public sealed class GameLibrarySyncService : IGameLibrarySyncService
{
    private readonly IGameRepository gameRepository;
    private readonly IGameLibraryRepository libraryRepository;
    private readonly ISettingsApi settingsApi;
    private readonly IReadOnlyList<IGameStoreIntegration> storeIntegrations;
    private readonly IReadOnlyList<IGameMetadataProvider> metadataProviders;
    private readonly IReadOnlyList<IGameArtworkProvider> artworkProviders;
    private readonly IGameArtworkStorage artworkStorage;
    private readonly IHowLongToBeatProvider howLongToBeatProvider;
    private readonly ILogger<GameLibrarySyncService> logger;
    private readonly SemaphoreSlim syncGate = new(1, 1);

    public GameLibrarySyncService(
        IGameRepository gameRepository,
        IGameLibraryRepository libraryRepository,
        ISettingsApi settingsApi,
        IEnumerable<IGameStoreIntegration> storeIntegrations,
        IEnumerable<IGameMetadataProvider> metadataProviders,
        IEnumerable<IGameArtworkProvider> artworkProviders,
        IGameArtworkStorage artworkStorage,
        IHowLongToBeatProvider howLongToBeatProvider,
        ILogger<GameLibrarySyncService> logger)
    {
        ArgumentNullException.ThrowIfNull(gameRepository);
        ArgumentNullException.ThrowIfNull(libraryRepository);
        ArgumentNullException.ThrowIfNull(settingsApi);
        ArgumentNullException.ThrowIfNull(storeIntegrations);
        ArgumentNullException.ThrowIfNull(metadataProviders);
        ArgumentNullException.ThrowIfNull(artworkProviders);
        ArgumentNullException.ThrowIfNull(artworkStorage);
        ArgumentNullException.ThrowIfNull(howLongToBeatProvider);
        ArgumentNullException.ThrowIfNull(logger);
        this.gameRepository = gameRepository;
        this.libraryRepository = libraryRepository;
        this.settingsApi = settingsApi;
        this.storeIntegrations = storeIntegrations.ToArray();
        this.metadataProviders = metadataProviders.ToArray();
        this.artworkProviders = artworkProviders.ToArray();
        this.artworkStorage = artworkStorage;
        this.howLongToBeatProvider = howLongToBeatProvider;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<Game>> SynchronizeAsync(
        string sourceId,
        IProgress<GameLibrarySyncProgress>? progress = null,
        CancellationToken cancellationToken = default,
        GameLibrarySyncMode mode = GameLibrarySyncMode.Full)
    {
        await syncGate.WaitAsync(cancellationToken);
        try
        {
            var integration = storeIntegrations.Single(item => item.SourceId == sourceId);
            progress?.Report(new GameLibrarySyncProgress(GameLibrarySyncStage.RetrievingCatalog));
            var library = await GetOrCreateLibraryAsync(cancellationToken);
            var importedGames = await integration.GetLibraryAsync(cancellationToken);
            var settings = await settingsApi.GetSectionAsync<GeneralSettings>(cancellationToken);

            var completedGames = 0;
            foreach (var importedGame in importedGames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var game = await SynchronizeGameAsync(
                    importedGame,
                    sourceId,
                    library.Id,
                    settings.LanguageTag,
                    cancellationToken,
                    mode);

                completedGames++;
                progress?.Report(new GameLibrarySyncProgress(
                    GameLibrarySyncStage.UpdatingGames,
                    completedGames,
                    importedGames.Count,
                    game.Name,
                    game));
            }

            return await gameRepository.GetAllAsync(cancellationToken);
        }
        finally
        {
            syncGate.Release();
        }
    }

    private async Task<GameLibrary> GetOrCreateLibraryAsync(CancellationToken cancellationToken)
    {
        var libraries = await libraryRepository.GetAllAsync(cancellationToken);
        var library = libraries.FirstOrDefault();
        if (library is not null)
        {
            return library;
        }

        return await CreateDefaultLibraryAsync(cancellationToken);
    }

    private async Task<Game> SynchronizeGameAsync(
        Game importedGame,
        string sourceId,
        Guid libraryId,
        string languageTag,
        CancellationToken cancellationToken,
        GameLibrarySyncMode mode)
    {
        importedGame.SourceId = sourceId;
        Game? existingGame = null;
        if (importedGame.SourceGameId is { } sourceGameId)
        {
            existingGame = await gameRepository.GetBySourceIdentityAsync(sourceId, sourceGameId, cancellationToken);
        }

        var game = importedGame;
        if (existingGame is not null)
        {
            existingGame.MergeStoreSnapshot(importedGame, mode == GameLibrarySyncMode.Full);
            game = existingGame;
        }

        if (!game.IsHidden &&
            (existingGame is null || mode == GameLibrarySyncMode.Full || NeedsEnrichment(game, sourceId)))
        {
            await EnrichImportedGameAsync(
                game,
                sourceId,
                languageTag,
                refreshAll: mode == GameLibrarySyncMode.Full,
                replaceMetadata: false,
                cancellationToken: cancellationToken);
        }
        if (existingGame is null)
        {
            await gameRepository.AddAsync(libraryId, game, cancellationToken);
        }
        else
        {
            await gameRepository.UpdateAsync(game, cancellationToken);
        }

        return game;
    }

    private bool NeedsEnrichment(Game game, string sourceId)
    {
        if (GetMetadataProvidersForGame(game, sourceId).Any(provider =>
                provider.CanSearch(game) && IsMetadataMissing(game, provider)))
        {
            return true;
        }

        var artworkProvider = GetArtworkProvider(game, sourceId);
        if (artworkProvider is not null && (game.DefaultArtwork is null || game.DefaultArtwork.IsEmpty))
        {
            return true;
        }

        return HasMissingCompatibilityTier(game, sourceId) || game.TimeToBeat is null;
    }

    public async Task RefreshMetadataAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsApi.GetSectionAsync<GeneralSettings>(cancellationToken);
        var games = await gameRepository.GetAllAsync(cancellationToken);
        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (game.IsHidden)
            {
                continue;
            }

            var updated = false;
            foreach (var provider in GetMetadataProvidersForGame(game, game.SourceId))
            {
                if (!provider.CanSearch(game))
                {
                    continue;
                }

                try
                {
                    var downloaded = await provider.GetMetadataAsync(game, settings.LanguageTag, cancellationToken);
                    if (downloaded is not null)
                    {
                        game.Metadata = game.Metadata.RefreshFromDownloaded(downloaded, settings.LanguageTag);
                        updated = true;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Metadata refresh from {Provider} failed for game {GameId} ({GameName}).", provider.ProviderId, game.Id, game.Name);
                }
            }

            updated |= await RefreshCompatibilityTierAsync(game, game.SourceId, cancellationToken);

            if (updated)
            {
                await gameRepository.UpdateAsync(game, cancellationToken);
            }
        }
    }

    public async Task<Game> RefreshGameDataAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await syncGate.WaitAsync(cancellationToken);
        try
        {
            var game = await gameRepository.GetByIdAsync(gameId, cancellationToken)
                ?? throw new KeyNotFoundException($"Game '{gameId}' was not found.");
            var settings = await settingsApi.GetSectionAsync<GeneralSettings>(cancellationToken);
            await EnrichImportedGameAsync(
                game,
                game.SourceId,
                settings.LanguageTag,
                refreshAll: true,
                replaceMetadata: true,
                cancellationToken: cancellationToken);
            await gameRepository.UpdateAsync(game, cancellationToken);
            return game;
        }
        finally
        {
            syncGate.Release();
        }
    }

    private async Task EnrichImportedGameAsync(
        Game game,
        string sourceId,
        string languageTag,
        bool refreshAll,
        bool replaceMetadata,
        CancellationToken cancellationToken)
    {
        foreach (var metadataProvider in GetMetadataProvidersForGame(game, sourceId))
        {
            if (!metadataProvider.CanSearch(game) || (!refreshAll && !IsMetadataMissing(game, metadataProvider)))
            {
                continue;
            }

            try
            {
                var downloaded = await metadataProvider.GetMetadataAsync(game, languageTag, cancellationToken);
                if (downloaded is not null)
                {
                    game.Metadata = replaceMetadata
                        ? game.Metadata.RefreshFromDownloaded(downloaded, languageTag)
                        : game.Metadata.MergeDownloaded(downloaded, languageTag);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Metadata lookup failed during sync for {GameName}.", game.Name);
            }
        }

        var artworkProvider = GetArtworkProvider(game, sourceId);
        if (artworkProvider is not null &&
            (refreshAll || game.DefaultArtwork is null || game.DefaultArtwork.IsEmpty))
        {
            try
            {
                var images = await artworkProvider.GetDefaultArtworksAsync(game, languageTag, cancellationToken);
                var defaults = await artworkStorage.StoreAsync(game.Id, images, game.DefaultArtwork ?? new GameArtwork(), cancellationToken);
                game.DefaultArtwork = defaults;
                if (game.Artwork.IsEmpty)
                {
                    game.Artwork = defaults;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Default artwork lookup failed during sync for {GameName}.", game.Name);
            }
        }

        await RefreshCompatibilityTierAsync(game, sourceId, cancellationToken, refreshAll);

        if (game.TimeToBeat is null || refreshAll)
        {
            try
            {
                var candidates = await howLongToBeatProvider.SearchAsync(game.Name, cancellationToken);
                game.TimeToBeat = GameTimeToBeat.FromSearchResult(HowLongToBeatGameMatcher.FindConfidentMatch(game, candidates));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                game.TimeToBeat = new GameTimeToBeat();
                logger.LogWarning(exception, "HowLongToBeat lookup failed during sync for {GameName}.", game.Name);
            }
        }
    }

    private async Task<bool> RefreshCompatibilityTierAsync(
        Game game,
        string sourceId,
        CancellationToken cancellationToken,
        bool refreshAll = false)
    {
        var updated = false;
        foreach (var metadataProvider in GetMetadataProvidersForGame(game, sourceId))
        {
            if (metadataProvider is not IGameCompatibilityTierProvider compatibilityProvider ||
                (!refreshAll && game.CompatibilityLayer?.Tier is not null and not GameCompatibilityTier.Unknown) ||
                !TryGetCompatibilityGameId(game, metadataProvider, sourceId, out var storeGameId))
            {
                continue;
            }

            try
            {
                var tier = await compatibilityProvider.GetCompatibilityTierAsync(storeGameId, cancellationToken);
                var layer = game.CompatibilityLayer ?? new CompatibilityLayer();
                layer.Tier = tier;
                game.CompatibilityLayer = layer;
                updated = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Compatibility lookup failed during sync for {GameName}.", game.Name);
            }
        }

        return updated;
    }

    private bool HasMissingCompatibilityTier(Game game, string sourceId) => GetMetadataProvidersForGame(game, sourceId)
        .Any(provider => provider is IGameCompatibilityTierProvider &&
                         TryGetCompatibilityGameId(game, provider, sourceId, out _) &&
                         game.CompatibilityLayer?.Tier is null or GameCompatibilityTier.Unknown);

    private static bool IsMetadataMissing(Game game, IGameMetadataProvider provider)
    {
        var hasStoreReference = !string.IsNullOrWhiteSpace(game.Metadata.StoreGameId) &&
                                (provider.SourceId is null || game.Metadata.StoreSourceId == provider.SourceId);
        return !hasStoreReference;
    }

    private static bool TryGetCompatibilityGameId(
        Game game,
        IGameMetadataProvider metadataProvider,
        string sourceId,
        out string storeGameId)
    {
        if (metadataProvider.SourceId is { } providerSourceId &&
            game.Metadata.StoreSourceId == providerSourceId &&
            !string.IsNullOrWhiteSpace(game.Metadata.StoreGameId))
        {
            storeGameId = game.Metadata.StoreGameId;
            return true;
        }

        if (metadataProvider.SourceId == sourceId &&
            game.SourceId == sourceId &&
            !string.IsNullOrWhiteSpace(game.SourceGameId))
        {
            storeGameId = game.SourceGameId;
            return true;
        }

        storeGameId = string.Empty;
        return false;
    }

    private async Task<GameLibrary> CreateDefaultLibraryAsync(CancellationToken cancellationToken)
    {
        var library = new GameLibrary();
        await libraryRepository.AddAsync(library, cancellationToken);
        return library;
    }

    private IEnumerable<IGameMetadataProvider> GetMetadataProvidersForGame(Game game, string sourceId) => metadataProviders
        .Where(provider => provider.SourceId == sourceId ||
                           provider.SourceId == game.Metadata.StoreSourceId ||
                           provider.SourceId is null)
        .OrderByDescending(provider => provider.SourceId == game.Metadata.StoreSourceId || provider.SourceId == sourceId);

    private IGameArtworkProvider? GetArtworkProvider(Game game, string sourceId)
    {
        var artworkSourceId = game.Metadata.StoreSourceId ?? sourceId;
        return artworkProviders.SingleOrDefault(provider => provider.SourceId == artworkSourceId);
    }
}
