using Hatband.Core.Enums.Games;

namespace Hatband.Core.Models.Games;

public sealed record GameLibrarySyncProgress(
    GameLibrarySyncStage Stage,
    int CompletedGames = 0,
    int TotalGames = 0,
    string? CurrentGameName = null,
    Game? UpdatedGame = null);
