using Hatband.Core.Models.Compatibility;
using Hatband.Core.Models.Games.PostInstall;

namespace Hatband.Core.Models.Games;

public sealed record GameRuntimeLaunchRequest(
    Guid GameId,
    CompatibilityTool? Tool,
    GameLaunchCommand Command,
    string? AdditionalArguments,
    IReadOnlyDictionary<string, string> UserEnvironmentVariables,
    string InstallDirectory,
    GamePostInstallPlan? PostInstallPlan,
    IProgress<GamePreparationProgress>? Progress = null);
