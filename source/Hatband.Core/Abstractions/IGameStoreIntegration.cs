using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

/// <summary>
/// Provides games from an external store or game platform.
/// </summary>
public interface IGameStoreIntegration
{
    /// <summary>
    /// Stable identifier shared with <see cref="Game.SourceId"/>.
    /// </summary>
    GameSourceId SourceId { get; }

    /// <summary>
    /// User-facing name of the store or platform.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Reads the games available from this source.
    /// </summary>
    Task<IReadOnlyList<Game>> GetLibraryAsync(CancellationToken cancellationToken = default);
}
