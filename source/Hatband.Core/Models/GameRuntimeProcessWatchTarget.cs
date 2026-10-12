using Hatband.Core.Enums.Games;

namespace Hatband.Core.Models;

public sealed record GameRuntimeProcessWatchTarget(
    Guid GameId,
    string ExecutablePath,
    Task<int> Completion,
    GameRuntimeProcessWatchMode Mode);
