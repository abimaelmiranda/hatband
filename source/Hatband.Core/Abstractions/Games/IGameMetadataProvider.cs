using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

/// <summary>
/// Searches for and provides descriptive metadata for games.
/// </summary>
public interface IGameMetadataProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    GameSourceId? SourceId { get; }

    bool CanSearch(Game game);

    Task<IReadOnlyList<GameMetadata>> SearchAsync(
        Game game,
        string languageTag,
        string region,
        CancellationToken cancellationToken = default);

    /// <param name="languageTag">The user's preferred BCP-47 language tag.</param>
    Task<GameMetadata?> GetMetadataAsync(
        Game game,
        string languageTag,
        CancellationToken cancellationToken = default);
}
