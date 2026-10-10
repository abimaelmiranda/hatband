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
}
