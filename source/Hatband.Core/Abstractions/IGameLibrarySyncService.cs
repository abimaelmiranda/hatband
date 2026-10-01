using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

/// <summary>
/// Imports a store library, then updates its artwork and descriptive metadata in separate stages.
/// </summary>
public interface IGameLibrarySyncService
{
    event EventHandler<GameLibraryEnrichmentProgressEventArgs>? LibraryEnrichmentProgressChanged;

    Task<IReadOnlyList<Game>> SynchronizeAsync(
        GameSourceId sourceId,
        CancellationToken cancellationToken = default);

    Task EnrichLibraryAsync(CancellationToken cancellationToken = default);
}
