using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

/// <summary>
/// Searches for remote artwork for a game.
/// </summary>
public interface IGameArtworkProvider
{
    string DisplayName { get; }

    GameSourceId? SourceId { get; }

    /// <summary>
    /// Gets the preferred artwork for the game's library source. An empty list means this provider has no default artwork.
    /// </summary>
    Task<IReadOnlyList<GameArtworkImage>> GetDefaultArtworksAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default);

    /// <param name="languageTag">The user's preferred BCP-47 language tag.</param>
    Task<IReadOnlyList<GameArtworkImage>> GetArtworksAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default);
}
