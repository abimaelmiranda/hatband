using Hatband.Core.Models.Compatibility;

namespace Hatband.Core.Models.Games;

public sealed record GameRuntimeLaunchRequest(
    Guid GameId,
    CompatibilityTool Tool,
    GameLaunchCommand Command,
    string? AdditionalArguments,
    IReadOnlyDictionary<string, string> UserEnvironmentVariables);
