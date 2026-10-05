using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

public interface IGameInstallationProvider
{
    GameSourceId SourceId { get; }

    /// <summary>
    /// Returns games installed locally and known to this provider.
    /// </summary>
    Task<IReadOnlyList<Game>> ScanInstalledGamesAsync(
        CancellationToken cancellationToken = default);
}
