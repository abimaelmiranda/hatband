namespace Hatband.Core.Models;

public sealed record GameRuntimeProcessWatchTarget(Guid GameId, string ExecutablePath, Task<int> Completion);
