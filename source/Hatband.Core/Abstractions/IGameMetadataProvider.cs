using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

/// <summary>
/// Provides optional descriptive metadata for games owned by a store.
/// </summary>
public interface IGameMetadataProvider
{
    GameSourceId SourceId { get; }

    /// <param name="languageTag">The user's preferred BCP-47 language tag.</param>
    Task<GameMetadata?> GetMetadataAsync(
        string sourceGameId,
        string languageTag,
        CancellationToken cancellationToken = default);
}
