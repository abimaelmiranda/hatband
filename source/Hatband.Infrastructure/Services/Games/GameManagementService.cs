using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Infrastructure.Services.Games;

public sealed class GameManagementService(IServiceProvider services) : IGameManagementService
{
    public Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(Game game, CancellationToken cancellationToken = default) =>
        TryGetService(game)?.GetInstallLocationsAsync(game, cancellationToken)
            ?? Task.FromResult<IReadOnlyList<GameInstallLocation>>([]);

    public Task<GameManagementResult> InstallAsync(
        Game game,
        GameInstallLocation? location = null,
        CancellationToken cancellationToken = default) =>
        TryGetService(game)?.InstallAsync(game, location, cancellationToken)
            ?? Task.FromResult(GameManagementResult.Unsupported);

    public Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default) =>
        TryGetService(game)?.UninstallAsync(game, cancellationToken)
            ?? Task.FromResult(GameManagementResult.Unsupported);

    public Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default) =>
        TryGetService(game)?.LaunchAsync(game, cancellationToken)
            ?? Task.FromResult(GameManagementResult.Unsupported);

    public GameProcessWatchTarget? GetProcessWatchTarget(Game game) => TryGetService(game)?.GetProcessWatchTarget(game);

    private IGameManagementService? TryGetService(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return services.GetKeyedService<IGameManagementService>(game.SourceId);
    }
}
