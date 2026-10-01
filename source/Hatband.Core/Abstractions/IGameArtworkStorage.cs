using Hatband.Core.Enums.Artwork;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

/// <summary>
/// Downloads and stores artwork outside the library database.
/// </summary>
public interface IGameArtworkStorage
{
    bool IsAvailable(string? path);

    Task<GameArtwork> StoreAsync(
        Guid gameId,
        GameArtworkSources sources,
        GameArtwork existingArtwork,
        CancellationToken cancellationToken = default);

    Task<string> ImportLocalAsync(
        Guid gameId,
        GameArtworkSlot slot,
        string sourcePath,
        CancellationToken cancellationToken = default);
}
