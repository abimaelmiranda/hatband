using Hatband.Core.Enums.Stores;
using Hatband.Core.Enums.Games;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

/// <summary>
/// Synchronizes a store library with its source metadata, default artwork, and HowLongToBeat data.
/// </summary>
public interface IGameLibrarySyncService
{
    Task<IReadOnlyList<Game>> SynchronizeAsync(
        string sourceId,
        IProgress<GameLibrarySyncProgress>? progress = null,
        CancellationToken cancellationToken = default,
        GameLibrarySyncMode mode = GameLibrarySyncMode.Full);

    /// <summary>
    /// Explicitly refreshes metadata for all games supported by their source providers.
    /// </summary>
    Task RefreshMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly refreshes metadata, artwork, compatibility, and play-time estimates for one game.
    /// </summary>
    Task<Game> RefreshGameDataAsync(Guid gameId, CancellationToken cancellationToken = default);
}
