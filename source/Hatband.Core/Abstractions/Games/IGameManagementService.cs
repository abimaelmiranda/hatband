using Hatband.Core.Enums.Games;
using Hatband.Core.Models;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

public interface IGameManagementService
{
    Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(Game game, CancellationToken cancellationToken = default);

    Task<GameManagementResult> InstallAsync(Game game, GameInstallLocation? location = null, CancellationToken cancellationToken = default);

    Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default);

    Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default);

    GameProcessWatchTarget? GetProcessWatchTarget(Game game);
}
