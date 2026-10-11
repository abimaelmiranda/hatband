using Hatband.Core.Enums.Games;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Models.Compatibility;

public class CompatibilityLayer
{
    public GameExecutionMode ExecutionMode { get; set; }

    public CompatibilityTool? Tool { get; set; }

    public string? LaunchOptionId { get; set; }

    public string? ExecutablePath { get; set; }

    public string? WorkingDirectory { get; set; }

    public string? LaunchArguments { get; set; }

    public string? AdditionalArguments { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];

    public GameCompatibilityTier Tier { get; set; } = GameCompatibilityTier.Unknown;

    public CompatibilityLayer Copy() => new()
    {
        ExecutionMode = ExecutionMode,
        Tool = Tool,
        LaunchOptionId = LaunchOptionId,
        ExecutablePath = ExecutablePath,
        WorkingDirectory = WorkingDirectory,
        LaunchArguments = LaunchArguments,
        AdditionalArguments = AdditionalArguments,
        EnvironmentVariables = EnvironmentVariables.ToDictionary(pair => pair.Key, pair => pair.Value),
        Tier = Tier
    };
}
