using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IGameInstallationProvider
{
    GameSourceId SourceId { get; }

    Task<IReadOnlyList<GameInstallationInfo>> ScanInstalledGamesAsync(
        CancellationToken cancellationToken = default);
}
