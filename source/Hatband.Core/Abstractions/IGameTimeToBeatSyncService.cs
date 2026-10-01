using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

/// <summary>
/// Updates cached HowLongToBeat data for games in the local library.
/// </summary>
public interface IGameTimeToBeatSyncService
{
    event EventHandler<GameTimeToBeatSyncProgressEventArgs>? ProgressChanged;

    Task SynchronizeAsync(CancellationToken cancellationToken = default);
}
