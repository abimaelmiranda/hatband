using Hatband.Core.Enums.Host;

namespace Hatband.Core.Models.Games;

public sealed record ExecutableGameLaunchPlan(
    GameLaunchCommand Command,
    HostOperatingSystem TargetOperatingSystem,
    string? RuntimeId = null) : GameLaunchPlan;
