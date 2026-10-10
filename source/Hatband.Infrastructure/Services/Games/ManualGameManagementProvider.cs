using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;
using Hatband.Core.Models.Games;

namespace Hatband.Infrastructure.Services.Games;

public sealed class ManualGameManagementProvider : IGameManagementProvider
{
    private readonly IHostApplicationLauncher _hostApplicationLauncher;

    public ManualGameManagementProvider(IHostApplicationLauncher hostApplicationLauncher)
    {
        ArgumentNullException.ThrowIfNull(hostApplicationLauncher);
        _hostApplicationLauncher = hostApplicationLauncher;
    }

    public GameSourceId SourceId => GameSourceId.Manual;

    public Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<GameInstallLocation>>([]);
    }

    public Task<GameManagementResult> InstallAsync(Game game, GameInstallLocation? location = null, CancellationToken cancellationToken = default)
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

    public async Task<GameLaunchResult> LaunchOriginalAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        var action = GetPrimaryAction(game);
        if (action.Type == GameActionType.Uri)
        {
            if (!Uri.TryCreate(action.Target, UriKind.Absolute, out var uri))
            {
                return new GameLaunchResult(GameManagementResult.Unavailable);
            }

            var opened = await _hostApplicationLauncher.TryOpenUriAsync(uri, cancellationToken);
            return new GameLaunchResult(opened ? GameManagementResult.ProtocolOpened : GameManagementResult.Unavailable);
        }

        var started = await _hostApplicationLauncher.TryLaunchApplicationAsync(
            action.Target,
            action.Arguments,
            action.WorkingDirectory,
            cancellationToken);
        if (!started)
        {
            return new GameLaunchResult(GameManagementResult.Unavailable);
        }

        var installDirectory = GetInstallDirectory(game, action.Target);
        var watchTarget = installDirectory is null ? null : new GameProcessWatchTarget(installDirectory);
        return new GameLaunchResult(GameManagementResult.GameActionStarted, watchTarget);
    }

    public Task<GameLaunchCommand> PrepareCustomLaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        var action = GetPrimaryAction(game);
        if (action.Type != GameActionType.Executable)
        {
            throw new InvalidOperationException("Custom Proton requires an executable launch action.");
        }

        ValidateExecutable(action.Target);
        var layer = game.CompatibilityLayer
            ?? throw new InvalidOperationException($"Custom compatibility is not configured for '{game.Name}'.");
        return Task.FromResult(new GameLaunchCommand(
            action.Target,
            action.Arguments,
            layer.WorkingDirectory ?? action.WorkingDirectory ?? Path.GetDirectoryName(action.Target),
            new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    public Task<IReadOnlyList<GameLaunchOption>> GetLaunchOptionsAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<GameLaunchOption>>([]);
    }

    private static GameAction GetPrimaryAction(Game game) =>
        game.GameActions.FirstOrDefault(action => action.IsPrimary)
        ?? throw new InvalidOperationException($"No primary launch action is configured for '{game.Name}'.");

    private static string? GetInstallDirectory(Game game, string executablePath)
    {
        var installDirectory = game.InstallationInfo?.InstallDirectory;
        if (!string.IsNullOrWhiteSpace(installDirectory) && Directory.Exists(installDirectory))
        {
            return installDirectory;
        }

        var executableDirectory = Path.GetDirectoryName(executablePath);
        return !string.IsNullOrWhiteSpace(executableDirectory) && Directory.Exists(executableDirectory)
            ? executableDirectory
            : null;
    }

    private static void ValidateExecutable(string executablePath)
    {
        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("The configured game executable must be an existing absolute path.", executablePath);
        }
    }
}
