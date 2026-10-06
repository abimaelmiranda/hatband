using Hatband.Core.Enums.Games;

namespace Hatband.Core.Abstractions.Games;

/// <summary>Provides a compatibility tier for a game associated with an external store ID.</summary>
public interface IGameCompatibilityTierProvider
{
    Task<GameCompatibilityTier> GetCompatibilityTierAsync(
        string storeGameId,
        CancellationToken cancellationToken = default);
}
