using Hatband.Core.Abstractions;
using Hatband.Core.Models;
using Hatband.Core.Services;
using Microsoft.Extensions.Logging;

namespace Hatband.Infrastructure.Persistence;

public sealed class GameTimeToBeatSyncService : IGameTimeToBeatSyncService
{
    private static readonly TimeSpan RequestInterval = TimeSpan.FromMilliseconds(900);

    private readonly SemaphoreSlim syncGate = new(1, 1);
    private readonly IGameLibraryService gameLibraryService;
    private readonly IHowLongToBeatProvider howLongToBeatProvider;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<GameTimeToBeatSyncService> logger;

    public GameTimeToBeatSyncService(
        IGameLibraryService gameLibraryService,
        IHowLongToBeatProvider howLongToBeatProvider,
        TimeProvider timeProvider,
        ILogger<GameTimeToBeatSyncService> logger)
    {
        ArgumentNullException.ThrowIfNull(gameLibraryService);
        ArgumentNullException.ThrowIfNull(howLongToBeatProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        this.gameLibraryService = gameLibraryService;
        this.howLongToBeatProvider = howLongToBeatProvider;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public event EventHandler<GameTimeToBeatSyncProgressEventArgs>? ProgressChanged;

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        await syncGate.WaitAsync(cancellationToken);
        try
        {
            var games = await gameLibraryService.GetGamesAsync(cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var gamesToSearch = games
                .Where(game => game.TimeToBeat?.IsSearchResultFresh(now) != true)
                .ToArray();

            var completedGames = 0;
            var failedGames = 0;
            RaiseProgress(completedGames, gamesToSearch.Length, failedGames, null, false);

            foreach (var game in gamesToSearch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var results = await howLongToBeatProvider.SearchAsync(game.Name, cancellationToken);
                    var match = HowLongToBeatGameMatcher.FindConfidentMatch(game, results);
                    var timeToBeat = GameTimeToBeat.FromSearchResult(match, timeProvider.GetUtcNow());
                    await gameLibraryService.UpdateGameTimeToBeatAsync(
                        game.Id,
                        timeToBeat,
                        cancellationToken);
                    game.TimeToBeat = timeToBeat;
                    RaiseProgress(
                        completedGames,
                        gamesToSearch.Length,
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
                        "Failed to synchronize HowLongToBeat data for game {GameId} ({GameName}).",
                        game.Id,
                        game.Name);
                    failedGames++;
                }

                completedGames++;
                RaiseProgress(
                    completedGames,
                    gamesToSearch.Length,
                    failedGames,
                    game.Name,
                    false);

                if (completedGames < gamesToSearch.Length)
                {
                    await Task.Delay(RequestInterval, cancellationToken);
                }
            }

            RaiseProgress(completedGames, gamesToSearch.Length, failedGames, null, true);
        }
        finally
        {
            syncGate.Release();
        }
    }

    private void RaiseProgress(
        int completedGames,
        int totalGames,
        int failedGames,
        string? currentGameName,
        bool isComplete,
        Game? updatedGame = null)
    {
        ProgressChanged?.Invoke(
            this,
            new GameTimeToBeatSyncProgressEventArgs(
                completedGames,
                totalGames,
                failedGames,
                currentGameName,
                isComplete,
                updatedGame));
    }
}
