using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

/// <summary>
/// Provides games from an external store or game platform.
/// </summary>
public interface IGameStoreIntegration : IGameSourceInfo
{
    /// <summary>
    /// Reads the games available from this source.
    /// </summary>
    Task<IReadOnlyList<Game>> GetLibraryAsync(CancellationToken cancellationToken = default);
}
