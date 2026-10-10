using Hatband.Core.Enums.Games;
using Hatband.Core.Models;

namespace Hatband.Core.Models.Games;

public sealed record GameLaunchResult(GameManagementResult Result, GameProcessWatchTarget? WatchTarget = null);
