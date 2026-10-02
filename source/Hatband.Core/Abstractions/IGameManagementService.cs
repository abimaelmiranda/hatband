using Hatband.Core.Enums;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IGameManagementService
{
    Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(Game game, CancellationToken cancellationToken = default);

    Task<GameManagementResult> InstallAsync(Game game, GameInstallLocation? location = null, CancellationToken cancellationToken = default);

    Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default);

    Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default);

    GameProcessWatchTarget? GetProcessWatchTarget(Game game);
}
