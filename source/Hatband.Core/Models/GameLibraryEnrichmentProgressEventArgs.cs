using Hatband.Core.Enums.Stores;
using Hatband.Core.Enums.Sync;

namespace Hatband.Core.Models;

public sealed class GameLibraryEnrichmentProgressEventArgs : EventArgs
{
    public GameLibraryEnrichmentProgressEventArgs(
        GameSourceId sourceId,
        GameLibrarySyncStage stage,
        int completedGames,
        int totalGames,
        int failedGames,
        string? currentGameName,
        bool isComplete,
        Game? updatedGame = null)
    {
        SourceId = sourceId;
        Stage = stage;
        CompletedGames = completedGames;
        TotalGames = totalGames;
        FailedGames = failedGames;
        CurrentGameName = currentGameName;
        IsComplete = isComplete;
        UpdatedGame = updatedGame;
    }

    public GameSourceId SourceId { get; }

    public GameLibrarySyncStage Stage { get; }

    public int CompletedGames { get; }

    public int TotalGames { get; }

    public int FailedGames { get; }

    public string? CurrentGameName { get; }

    public bool IsComplete { get; }

    public Game? UpdatedGame { get; }
}
