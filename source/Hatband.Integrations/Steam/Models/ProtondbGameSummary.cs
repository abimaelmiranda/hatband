using System;
using System.Text.Json.Serialization;
using Hatband.Core.Enums.Games;

namespace Hatband.Integrations.Steam.Models;

public record ProtondbGameSummary
{
    [JsonPropertyName("tier")]
    public string Tier { get; init; } = string.Empty;


    public GameCompatibilityTier ToGameCompatibilityTier()
    {
        return Tier.ToLowerInvariant() switch
        {
            "platinum" => GameCompatibilityTier.Platinum,
            "gold" => GameCompatibilityTier.Gold,
            "silver" => GameCompatibilityTier.Silver,
            "bronze" => GameCompatibilityTier.Bronze,
            "borked" => GameCompatibilityTier.Borked,
            _ => GameCompatibilityTier.Unknown
        };
    }
}
