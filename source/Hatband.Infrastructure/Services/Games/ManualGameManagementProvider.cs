using Hatband.Core.Abstractions.Host;

namespace Hatband.Infrastructure.Services.Games;

public sealed class ManualGameManagementProvider : IGameManagementService
{
    private readonly IHostApplicationLauncher _hostApplicationLauncher;

    public ManualGameManagementProvider(IHostApplicationLauncher hostApplicationLauncher)
    {
        ArgumentNullException.ThrowIfNull(hostApplicationLauncher);
        _hostApplicationLauncher = hostApplicationLauncher;
    }

    public Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<GameInstallLocation>>([]);
    }

    public Task<GameManagementResult> InstallAsync(
        Game game,
        GameInstallLocation? location = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GameManagementResult.Unsupported);
    }

    public Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GameManagementResult.Unsupported);
    }

    public async Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        var primaryAction = game.GameActions.FirstOrDefault(action => action.IsPrimary);
        if (primaryAction is null)
        {
            return GameManagementResult.Unsupported;
        }

        if (primaryAction.Type == GameActionType.Uri)
        {
            if (!Uri.TryCreate(primaryAction.Target, UriKind.Absolute, out var uri))
            {
                return GameManagementResult.Unavailable;
            }

            var opened = await _hostApplicationLauncher.TryOpenUriAsync(uri, cancellationToken);
            return opened ? GameManagementResult.ProtocolOpened : GameManagementResult.Unavailable;
        }

        if (primaryAction.Type != GameActionType.Executable)
        {
            return GameManagementResult.Unsupported;
        }

        var started = await _hostApplicationLauncher.TryLaunchApplicationAsync(
            primaryAction.Target,
            primaryAction.Arguments,
            primaryAction.WorkingDirectory,
            cancellationToken);

        return started ? GameManagementResult.GameActionStarted : GameManagementResult.Unavailable;
    }

    public GameProcessWatchTarget? GetProcessWatchTarget(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var installDirectory = game.InstallationInfo?.InstallDirectory;
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            return null;
        }

        return new GameProcessWatchTarget(installDirectory);
    }
}
