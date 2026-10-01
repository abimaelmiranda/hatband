using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Enums.Sync;
using Hatband.Core.Extensions;
using Hatband.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hatband.Infrastructure.Persistence;

internal sealed class GameLibraryEnrichmentService
{
    private static readonly TimeSpan RequestInterval = TimeSpan.FromMilliseconds(300);

    private readonly IGameLibraryService gameLibraryService;
    private readonly IGameArtworkStorage artworkStorage;
    private readonly ILogger<GameLibraryEnrichmentService> logger;

    public GameLibraryEnrichmentService(
        IGameLibraryService gameLibraryService,
        IGameArtworkStorage artworkStorage,
        ILogger<GameLibraryEnrichmentService> logger)
    {
        ArgumentNullException.ThrowIfNull(gameLibraryService);
        ArgumentNullException.ThrowIfNull(artworkStorage);
        ArgumentNullException.ThrowIfNull(logger);
        this.gameLibraryService = gameLibraryService;
        this.artworkStorage = artworkStorage;
        this.logger = logger;
    }

    public bool NeedsEnrichment(
        Game game,
        IGameArtworkProvider? artworkProvider,
        IGameMetadataProvider? metadataProvider,
        string languageTag)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);

        return (artworkProvider is not null && NeedsArtwork(game)) ||
               (metadataProvider is not null &&
                game.Metadata.NeedsStoreRefresh(game.Name, game.IsNameCustomized, languageTag));
    }

    public async Task EnrichAsync(
        GameSourceId sourceId,
        IReadOnlyList<Game> games,
        IGameArtworkProvider? artworkProvider,
        IGameMetadataProvider? metadataProvider,
        string languageTag,
        CancellationToken cancellationToken,
        Action<GameLibraryEnrichmentProgressEventArgs> reportProgress)
    {
        ArgumentNullException.ThrowIfNull(games);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        ArgumentNullException.ThrowIfNull(reportProgress);

        var failedGames = 0;
        var artworkGames = artworkProvider is null
            ? Array.Empty<Game>()
            : games.Where(NeedsArtwork).ToArray();

        if (artworkProvider is not null)
        {
            failedGames = await DownloadArtworkAsync(
                sourceId,
                artworkGames,
                artworkProvider,
                languageTag,
                cancellationToken,
                reportProgress,
                failedGames);
        }

        var metadataGames = metadataProvider is null
            ? Array.Empty<Game>()
            : games.Where(game => game.Metadata.NeedsStoreRefresh(
                game.Name,
                game.IsNameCustomized,
                languageTag)).ToArray();

        if (metadataProvider is not null)
        {
            failedGames = await DownloadMetadataAsync(
                sourceId,
                metadataGames,
                metadataProvider,
                languageTag,
                cancellationToken,
                reportProgress,
                failedGames);
        }

        ReportProgress(
            reportProgress,
            sourceId,
            GameLibrarySyncStage.Metadata,
            metadataGames.Length,
            metadataGames.Length,
            failedGames,
            null,
            true);
    }

    private async Task<int> DownloadArtworkAsync(
        GameSourceId sourceId,
        IReadOnlyList<Game> games,
        IGameArtworkProvider artworkProvider,
        string languageTag,
        CancellationToken cancellationToken,
        Action<GameLibraryEnrichmentProgressEventArgs> reportProgress,
        int failedGames)
    {
        ReportProgress(reportProgress, sourceId, GameLibrarySyncStage.Artwork, 0, games.Count, failedGames, null, false);
        var completedGames = 0;

        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Game? updatedGame = null;
            try
            {
                if (game.SourceGameId is null)
                {
                    throw new InvalidOperationException("The source game ID is required to look up artwork.");
                }

                var sources = await artworkProvider.GetArtworkSourcesAsync(
                    game.SourceGameId,
                    languageTag,
                    cancellationToken);
                if (sources is null)
                {
                    throw new InvalidOperationException($"No artwork locations were returned for '{game.Name}'.");
                }

                sources = PreserveCustomizedArtworkSources(game.Metadata.Artwork, sources);
                var artwork = await artworkStorage.StoreAsync(
                    game.Id,
                    sources,
                    game.Metadata.Artwork,
                    cancellationToken);
                var updatedMetadata = game.Metadata with
                {
                    Artwork = artwork with
                    {
                        IsCoverCustomized = game.Metadata.Artwork.IsCoverCustomized,
                        IsBackgroundCustomized = game.Metadata.Artwork.IsBackgroundCustomized,
                        IsIconCustomized = game.Metadata.Artwork.IsIconCustomized
                    }
                };
                await gameLibraryService.UpdateGameMetadataAsync(game.Id, updatedMetadata, cancellationToken);
                game.Metadata = updatedMetadata;
                updatedGame = game;

                if (NeedsArtwork(artwork))
                {
                    logger.LogWarning(
                        "Artwork enrichment left one or more required artwork slots unavailable for game {GameId} ({GameName}).",
                        game.Id,
                        game.Name);
                    failedGames++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to enrich artwork for game {GameId} ({GameName}) from source {SourceId}.",
                    game.Id,
                    game.Name,
                    sourceId);
                failedGames++;
            }

            completedGames++;
            ReportProgress(
                reportProgress,
                sourceId,
                GameLibrarySyncStage.Artwork,
                completedGames,
                games.Count,
                failedGames,
                game.Name,
                false,
                updatedGame);

            if (completedGames < games.Count)
            {
                await Task.Delay(RequestInterval, cancellationToken);
            }
        }

        return failedGames;
    }

    private async Task<int> DownloadMetadataAsync(
        GameSourceId sourceId,
        IReadOnlyList<Game> games,
        IGameMetadataProvider metadataProvider,
        string languageTag,
        CancellationToken cancellationToken,
        Action<GameLibraryEnrichmentProgressEventArgs> reportProgress,
        int failedGames)
    {
        ReportProgress(reportProgress, sourceId, GameLibrarySyncStage.Metadata, 0, games.Count, failedGames, null, false);
        var completedGames = 0;

        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (game.SourceGameId is null)
                {
                    throw new InvalidOperationException("The source game ID is required to look up metadata.");
                }

                var downloadedMetadata = await metadataProvider.GetMetadataAsync(
                    game.SourceGameId,
                    languageTag,
                    cancellationToken);
                if (downloadedMetadata is null)
                {
                    throw new InvalidOperationException($"No metadata was returned for '{game.Name}'.");
                }

                var metadata = game.Metadata.MergeDownloaded(downloadedMetadata, languageTag);
                var gameName = game.IsNameCustomized
                    ? game.Name
                    : metadata.StoreName.PreferNonWhiteSpace(game.Name) ?? game.Name;
                await gameLibraryService.UpdateGameDetailsAsync(
                    game.Id,
                    gameName,
                    game.IsNameCustomized,
                    metadata,
                    cancellationToken);
                game.Name = gameName;
                game.Metadata = metadata;
                ReportProgress(
                    reportProgress,
                    sourceId,
                    GameLibrarySyncStage.Metadata,
                    completedGames,
                    games.Count,
                    failedGames,
                    game.Name,
                    false,
                    game);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to enrich metadata for game {GameId} ({GameName}) from source {SourceId}.",
                    game.Id,
                    game.Name,
                    sourceId);
                failedGames++;
            }

            completedGames++;
            ReportProgress(
                reportProgress,
                sourceId,
                GameLibrarySyncStage.Metadata,
                completedGames,
                games.Count,
                failedGames,
                game.Name,
                false);

            if (completedGames < games.Count)
            {
                await Task.Delay(RequestInterval, cancellationToken);
            }
        }

        return failedGames;
    }

    private bool NeedsArtwork(Game game)
    {
        return NeedsArtwork(game.Metadata.Artwork);
    }

    private bool NeedsArtwork(GameArtwork artwork)
    {
        if (!artwork.IsCoverCustomized && !artworkStorage.IsAvailable(artwork.CoverImagePath))
        {
            return true;
        }

        if (!artwork.IsBackgroundCustomized && !artworkStorage.IsAvailable(artwork.BackgroundImagePath))
        {
            return true;
        }

        return !artwork.IsIconCustomized &&
               !string.IsNullOrWhiteSpace(artwork.IconPath) &&
               !artworkStorage.IsAvailable(artwork.IconPath);
    }

    private static GameArtworkSources PreserveCustomizedArtworkSources(
        GameArtwork artwork,
        GameArtworkSources sources)
    {
        return new GameArtworkSources
        {
            CoverImageUrls = artwork.IsCoverCustomized ? Array.Empty<string>() : sources.CoverImageUrls,
            BackgroundImageUrls = artwork.IsBackgroundCustomized ? Array.Empty<string>() : sources.BackgroundImageUrls,
            IconUrls = artwork.IsIconCustomized ? Array.Empty<string>() : sources.IconUrls
        };
    }

    private static void ReportProgress(
        Action<GameLibraryEnrichmentProgressEventArgs> reportProgress,
        GameSourceId sourceId,
        GameLibrarySyncStage stage,
        int completedGames,
        int totalGames,
        int failedGames,
        string? currentGameName,
        bool isComplete,
        Game? updatedGame = null)
    {
        reportProgress(new GameLibraryEnrichmentProgressEventArgs(
            sourceId,
            stage,
            completedGames,
            totalGames,
            failedGames,
            currentGameName,
            isComplete,
            updatedGame));
    }
}
