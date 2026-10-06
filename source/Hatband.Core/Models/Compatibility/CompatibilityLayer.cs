using Hatband.Core.Enums.Games;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Models.Compatibility;

public class CompatibilityLayer
{
    public CompatibilityTool? Tool { get; set; }

    public GameCompatibilityPrefix? Prefix { get; set; }

    public GameCompatibilityTier Tier { get; set; } = GameCompatibilityTier.Unknown;
}
