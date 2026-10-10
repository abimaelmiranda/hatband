using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Games;

public interface IGameManagementProvider
{
    GameSourceId SourceId { get; }

    Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(Game game, CancellationToken cancellationToken = default);

    Task<GameManagementResult> InstallAsync(Game game, GameInstallLocation? location = null, CancellationToken cancellationToken = default);

    Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default);

    Task<GameLaunchResult> LaunchOriginalAsync(Game game, CancellationToken cancellationToken = default);

    Task<GameLaunchCommand> PrepareCustomLaunchAsync(Game game, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameLaunchOption>> GetLaunchOptionsAsync(Game game, CancellationToken cancellationToken = default);
}
