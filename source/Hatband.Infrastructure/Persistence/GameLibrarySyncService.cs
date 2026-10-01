using Hatband.Core.Abstractions;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hatband.Infrastructure.Persistence;

public sealed class GameLibrarySyncService : IGameLibrarySyncService, IDisposable
{
    private static readonly TimeSpan SourceWaitInterval = TimeSpan.FromMilliseconds(100);

    private readonly Lock syncLock = new();
    private readonly IGameLibraryService gameLibraryService;
    private readonly ISettingsStore settingsStore;
    private readonly IReadOnlyList<IGameStoreIntegration> storeIntegrations;
    private readonly IReadOnlyList<IGameMetadataProvider> metadataProviders;
    private readonly IReadOnlyList<IGameArtworkProvider> artworkProviders;
    private readonly GameLibraryEnrichmentService enrichmentService;
    private readonly HashSet<GameSourceId> activeSources = [];
    private readonly Dictionary<GameSourceId, CancellationTokenSource> enrichmentCancellations = [];
    private bool isDisposed;

    public GameLibrarySyncService(
        IGameLibraryService gameLibraryService,
        ISettingsStore settingsStore,
        IEnumerable<IGameStoreIntegration> storeIntegrations,
        IEnumerable<IGameMetadataProvider> metadataProviders,
        IEnumerable<IGameArtworkProvider> artworkProviders,
        IGameArtworkStorage artworkStorage,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(gameLibraryService);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(storeIntegrations);
        ArgumentNullException.ThrowIfNull(metadataProviders);
        ArgumentNullException.ThrowIfNull(artworkProviders);
        ArgumentNullException.ThrowIfNull(artworkStorage);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.gameLibraryService = gameLibraryService;
        this.settingsStore = settingsStore;
        this.storeIntegrations = storeIntegrations.ToArray();
        this.metadataProviders = metadataProviders.ToArray();
        this.artworkProviders = artworkProviders.ToArray();
        enrichmentService = new GameLibraryEnrichmentService(
            gameLibraryService,
            artworkStorage,
            loggerFactory.CreateLogger<GameLibraryEnrichmentService>());
    }

    public event EventHandler<GameLibraryEnrichmentProgressEventArgs>? LibraryEnrichmentProgressChanged;

    public async Task EnrichLibraryAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        var settings = await settingsStore.LoadAsync(cancellationToken);
        var languageTag = settings.General.LanguageTag;
        var savedGames = await gameLibraryService.GetGamesAsync(cancellationToken);
        var gamesBySource = savedGames
            .Where(game => game.SourceId is not null && game.SourceGameId is not null)
            .GroupBy(game => game.SourceId ?? throw new InvalidOperationException("A game source is required."));

        foreach (var gamesForSource in gamesBySource)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceId = gamesForSource.Key;
            var artworkProvider = artworkProviders.SingleOrDefault(provider => provider.SourceId == sourceId);
            var metadataProvider = metadataProviders.SingleOrDefault(provider => provider.SourceId == sourceId);
            var gamesToUpdate = gamesForSource
                .Where(game => enrichmentService.NeedsEnrichment(
                    game,
                    artworkProvider,
                    metadataProvider,
                    languageTag))
                .ToArray();

            if (gamesToUpdate.Length == 0)
            {
                continue;
            }

            var enrichmentCancellation = await StartSourceEnrichmentAsync(sourceId, cancellationToken);
            try
            {
                var refreshedGames = await gameLibraryService.GetGamesAsync(cancellationToken);
                var currentGamesForSource = refreshedGames
                    .Where(game => game.SourceId == sourceId && game.SourceGameId is not null)
                    .Where(game => enrichmentService.NeedsEnrichment(
                        game,
                        artworkProvider,
                        metadataProvider,
                        languageTag))
                    .ToArray();

                await EnrichGamesAsync(
                    sourceId,
                    currentGamesForSource,
                    artworkProvider,
                    metadataProvider,
                    languageTag,
                    enrichmentCancellation);
            }
            finally
            {
                FinishSourceEnrichment(sourceId, enrichmentCancellation);
            }
        }
    }

    public async Task<IReadOnlyList<Game>> SynchronizeAsync(
        GameSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        var languageTag = settings.General.LanguageTag;
        var enrichmentCancellation = await StartSourceEnrichmentAsync(sourceId, cancellationToken);

        try
        {
            var storeIntegration = storeIntegrations.Single(integration => integration.SourceId == sourceId);
            var importedGames = await storeIntegration.GetLibraryAsync(cancellationToken);
            await gameLibraryService.SynchronizeGamesAsync(sourceId, importedGames, cancellationToken);

            var savedGames = await gameLibraryService.GetGamesAsync(cancellationToken);
            var artworkProvider = artworkProviders.SingleOrDefault(provider => provider.SourceId == sourceId);
            var metadataProvider = metadataProviders.SingleOrDefault(provider => provider.SourceId == sourceId);
            var gamesToUpdate = savedGames
                .Where(game => game.SourceId == sourceId &&
                               enrichmentService.NeedsEnrichment(
                                   game,
                                   artworkProvider,
                                   metadataProvider,
                                   languageTag))
                .ToArray();

            if (gamesToUpdate.Length > 0)
            {
                await EnrichGamesAsync(
                    sourceId,
                    gamesToUpdate,
                    artworkProvider,
                    metadataProvider,
                    languageTag,
                    enrichmentCancellation);
            }

            return await gameLibraryService.GetGamesAsync(cancellationToken);
        }
        finally
        {
            FinishSourceEnrichment(sourceId, enrichmentCancellation);
        }
    }

    private Task EnrichGamesAsync(
        GameSourceId sourceId,
        IReadOnlyList<Game> games,
        IGameArtworkProvider? artworkProvider,
        IGameMetadataProvider? metadataProvider,
        string languageTag,
        CancellationTokenSource enrichmentCancellation)
    {
        return enrichmentService.EnrichAsync(
            sourceId,
            games,
            artworkProvider,
            metadataProvider,
            languageTag,
            enrichmentCancellation.Token,
            PublishEnrichmentProgress);
    }

    private void PublishEnrichmentProgress(GameLibraryEnrichmentProgressEventArgs eventArgs)
    {
        LibraryEnrichmentProgressChanged?.Invoke(this, eventArgs);
    }

    private async Task<CancellationTokenSource> StartSourceEnrichmentAsync(
        GameSourceId sourceId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (syncLock)
            {
                ObjectDisposedException.ThrowIf(isDisposed, this);
                if (activeSources.Add(sourceId))
                {
                    var enrichmentCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    enrichmentCancellations.Add(sourceId, enrichmentCancellation);
                    return enrichmentCancellation;
                }
            }

            await Task.Delay(SourceWaitInterval, cancellationToken);
        }
    }

    private void FinishSourceEnrichment(GameSourceId sourceId, CancellationTokenSource enrichmentCancellation)
    {
        lock (syncLock)
        {
            activeSources.Remove(sourceId);
            enrichmentCancellations.Remove(sourceId);
        }

        enrichmentCancellation.Dispose();
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            foreach (var cancellation in enrichmentCancellations.Values)
            {
                cancellation.Cancel();
            }
        }
    }
}
