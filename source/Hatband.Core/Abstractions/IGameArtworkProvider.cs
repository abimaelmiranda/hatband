using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

/// <summary>
/// Provides remote artwork locations for games owned by a store.
/// </summary>
public interface IGameArtworkProvider
{
    GameSourceId SourceId { get; }

    /// <param name="languageTag">The user's preferred BCP-47 language tag.</param>
    Task<GameArtworkSources?> GetArtworkSourcesAsync(
        string sourceGameId,
        string languageTag,
        CancellationToken cancellationToken = default);
}
