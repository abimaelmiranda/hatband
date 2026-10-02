using Hatband.Core.Enums;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IGameManagementService
{
    Task<GameManagementResult> InstallAsync(Game game, CancellationToken cancellationToken = default);

    Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default);

    Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default);

    GameProcessWatchTarget? GetProcessWatchTarget(Game game);
}
