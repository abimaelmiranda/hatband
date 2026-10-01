namespace Hatband.Core.Models;

public sealed class GameTimeToBeatSyncProgressEventArgs : EventArgs
{
    public GameTimeToBeatSyncProgressEventArgs(
        int completedGames,
        int totalGames,
        int failedGames,
        string? currentGameName,
        bool isComplete,
        Game? updatedGame = null)
    {
        CompletedGames = completedGames;
        TotalGames = totalGames;
        FailedGames = failedGames;
        CurrentGameName = currentGameName;
        IsComplete = isComplete;
        UpdatedGame = updatedGame;
    }

    public int CompletedGames { get; }

    public int TotalGames { get; }

    public int FailedGames { get; }

    public string? CurrentGameName { get; }

    public bool IsComplete { get; }

    public Game? UpdatedGame { get; }
}
