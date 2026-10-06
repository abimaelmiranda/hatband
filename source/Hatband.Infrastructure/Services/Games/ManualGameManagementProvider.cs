using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Host;
using Hatband.Infrastructure.Services.CompatibilityTools;

namespace Hatband.Infrastructure.Services.Games;

public sealed class ManualGameManagementProvider : IGameManagementService
{
    private readonly IHostApplicationLauncher _hostApplicationLauncher;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly ProtonExecutionService _protonExecutionService;

    public ManualGameManagementProvider(
        IHostApplicationLauncher hostApplicationLauncher,
        IHostSystemInfo hostSystemInfo,
        ProtonExecutionService protonExecutionService)
    {
        ArgumentNullException.ThrowIfNull(hostApplicationLauncher);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(protonExecutionService);
        _hostApplicationLauncher = hostApplicationLauncher;
        _hostSystemInfo = hostSystemInfo;
        _protonExecutionService = protonExecutionService;
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

        if (_hostSystemInfo.Platform == HostOperatingSystem.Linux && game.CompatibilityTool is not null)
        {
            var protonStarted = await _protonExecutionService.LaunchAsync(
                game,
                primaryAction.Target,
                primaryAction.Arguments,
                primaryAction.WorkingDirectory,
                cancellationToken);
            return protonStarted ? GameManagementResult.GameActionStarted : GameManagementResult.Unavailable;
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

        if (_hostSystemInfo.Platform == HostOperatingSystem.Linux && game.CompatibilityTool is not null)
        {
            var primaryAction = game.GameActions.FirstOrDefault(action => action.IsPrimary);
            if (primaryAction?.Type != GameActionType.Executable)
            {
                return null;
            }

            var executablePath = primaryAction.Target;
            if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
            {
                return null;
            }

            var protonInstallDirectory = game.InstallationInfo?.InstallDirectory;
            if (string.IsNullOrWhiteSpace(protonInstallDirectory) || !Directory.Exists(protonInstallDirectory))
            {
                protonInstallDirectory = Path.GetDirectoryName(executablePath);
            }

            if (string.IsNullOrWhiteSpace(protonInstallDirectory))
            {
                return null;
            }

            return new GameProcessWatchTarget(
                protonInstallDirectory,
                new ProtonProcessWatchTarget(game.Id, executablePath));
        }

        var installDirectory = game.InstallationInfo?.InstallDirectory;
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            return null;
        }

        return new GameProcessWatchTarget(installDirectory);
    }
}
