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
        GameSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        await syncGate.WaitAsync(cancellationToken);
        try
        {
            var integration = storeIntegrations.Single(item => item.SourceId == sourceId);
            var libraries = await libraryRepository.GetAllAsync(cancellationToken);
            var library = libraries.FirstOrDefault() ?? await CreateDefaultLibraryAsync(cancellationToken);
            var importedGames = await integration.GetLibraryAsync(cancellationToken);
            var settings = await settingsApi.GetSectionAsync<GeneralSettings>(cancellationToken);
            foreach (var importedGame in importedGames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                importedGame.SourceId = sourceId;
                var existingGame = importedGame.SourceGameId is { } sourceGameId
                    ? await gameRepository.GetBySourceIdentityAsync(sourceId, sourceGameId, cancellationToken)
                    : null;
                var game = existingGame is null ? importedGame : MergeImportedGame(existingGame, importedGame);
                await EnrichImportedGameAsync(game, sourceId, settings.LanguageTag, cancellationToken);
                if (existingGame is null)
                {
                    await gameRepository.AddAsync(library.Id, game, cancellationToken);
                }
                else
                {
                    await gameRepository.UpdateAsync(game, cancellationToken);
                }
            }

            return await gameRepository.GetAllAsync(cancellationToken);
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task RefreshMetadataAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsApi.GetSectionAsync<GeneralSettings>(cancellationToken);
        var games = await gameRepository.GetAllAsync(cancellationToken);
        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var updated = false;
            foreach (var provider in GetMetadataProviders(game.SourceId))
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

            if (updated)
            {
                await gameRepository.UpdateAsync(game, cancellationToken);
            }
        }
    }

    private async Task EnrichImportedGameAsync(Game game, GameSourceId sourceId, string languageTag, CancellationToken cancellationToken)
    {
        foreach (var metadataProvider in GetMetadataProviders(sourceId))
        {
            if (!metadataProvider.CanSearch(game))
            {
                continue;
            }

            try
            {
                var downloaded = await metadataProvider.GetMetadataAsync(game, languageTag, cancellationToken);
                if (downloaded is not null)
                {
                    game.Metadata = game.Metadata.MergeDownloaded(downloaded, languageTag);
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

        var artworkProvider = artworkProviders.SingleOrDefault(item => item.SourceId == sourceId);
        if (artworkProvider is not null)
        {
            try
            {
                var images = await artworkProvider.GetDefaultArtworksAsync(game, languageTag, cancellationToken);
                var defaults = await artworkStorage.StoreAsync(game.Id, images, game.DefaultArtwork ?? new GameArtwork(), cancellationToken);
                game.DefaultArtwork = defaults;
                if (game.Artwork is null || IsArtworkEmpty(game.Artwork))
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

        if (game.TimeToBeat is null)
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

    private async Task<GameLibrary> CreateDefaultLibraryAsync(CancellationToken cancellationToken)
    {
        var library = new GameLibrary();
        await libraryRepository.AddAsync(library, cancellationToken);
        return library;
    }

    private static Game MergeImportedGame(Game existingGame, Game importedGame)
    {
        existingGame.Metadata = existingGame.Metadata.MergeDownloaded(importedGame.Metadata, importedGame.Metadata.LanguageTag ?? "en-US");
        existingGame.InstallationInfo = importedGame.InstallationInfo;
        existingGame.Version = importedGame.Version;
        existingGame.InstallSizeBytes = importedGame.InstallSizeBytes;
        existingGame.PlaytimeSeconds = importedGame.PlaytimeSeconds;
        existingGame.PlayCount = importedGame.PlayCount;
        existingGame.LastActivity = importedGame.LastActivity;
        existingGame.Added ??= importedGame.Added;
        if (existingGame.GameActions.Count == 0)
        {
            existingGame.GameActions = importedGame.GameActions;
        }

        return existingGame;
    }

    private static bool IsArtworkEmpty(GameArtwork artwork) =>
        artwork.CoverImagePath is null && artwork.BackgroundImagePath is null && artwork.IconPath is null;

    private IEnumerable<IGameMetadataProvider> GetMetadataProviders(GameSourceId sourceId) => metadataProviders
        .Where(provider => provider.SourceId == sourceId || provider.SourceId is null)
        .OrderByDescending(provider => provider.SourceId == sourceId);
}
