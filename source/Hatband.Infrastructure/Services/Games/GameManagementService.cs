using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Enums.Games;
using Hatband.Core.Enums.Host;
using Hatband.Core.Models;
using Hatband.Core.Models.Games;

namespace Hatband.Infrastructure.Services.Games;

public sealed class GameManagementService : IGameManagementService
{
    private readonly IReadOnlyDictionary<GameSourceId, IGameManagementProvider> _providers;
    private readonly IGameCompatibilityRuntime _runtime;
    private readonly IHostSystemInfo _hostSystemInfo;

    public GameManagementService(
        IEnumerable<IGameManagementProvider> providers,
        IGameCompatibilityRuntime runtime,
        IHostSystemInfo hostSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        _providers = providers.ToDictionary(provider => provider.SourceId);
        _runtime = runtime;
        _hostSystemInfo = hostSystemInfo;
    }

    public Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(Game game, CancellationToken cancellationToken = default) =>
        GetProvider(game).GetInstallLocationsAsync(game, cancellationToken);

    public Task<GameManagementResult> InstallAsync(Game game, GameInstallLocation? location = null, CancellationToken cancellationToken = default) =>
        GetProvider(game).InstallAsync(game, location, cancellationToken);

    public Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default) =>
        GetProvider(game).UninstallAsync(game, cancellationToken);

    public Task<IReadOnlyList<GameLaunchOption>> GetLaunchOptionsAsync(Game game, CancellationToken cancellationToken = default) =>
        GetProvider(game).GetLaunchOptionsAsync(game, cancellationToken);

    public async Task<GameLaunchResult> LaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var provider = GetProvider(game);
        if (game.CompatibilityLayer is { ExecutionMode: GameExecutionMode.Custom } compatibilityLayer &&
            _hostSystemInfo.Platform == HostOperatingSystem.Linux)
        {
            var tool = game.GetCompatibilityTool()
                ?? throw new InvalidOperationException($"Custom compatibility is enabled for '{game.Name}', but no Proton tool is configured.");
            var environmentVariables = compatibilityLayer.EnvironmentVariables;
            GameLaunchEnvironmentPolicy.ValidateUserVariables(environmentVariables);
            var command = await provider.PrepareCustomLaunchAsync(game, cancellationToken);
            var installDirectory = game.InstallationInfo?.InstallDirectory;
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                installDirectory = Path.GetDirectoryName(command.ExecutablePath);
            }

            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new InvalidOperationException($"No install directory can be determined for '{game.Name}'.");
            }

            var process = await _runtime.LaunchAsync(
                new GameRuntimeLaunchRequest(
                    game.Id,
                    tool,
                    command,
                    compatibilityLayer.AdditionalArguments,
                    environmentVariables),
                cancellationToken);
            if (process is null)
            {
                return new GameLaunchResult(GameManagementResult.Unavailable);
            }

            var watchTarget = new GameProcessWatchTarget(
                installDirectory,
                new GameRuntimeProcessWatchTarget(game.Id, command.ExecutablePath, process.Completion));
            return new GameLaunchResult(GameManagementResult.GameActionStarted, watchTarget);
        }

        return await provider.LaunchOriginalAsync(game, cancellationToken);
    }

    private IGameManagementProvider GetProvider(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (_providers.TryGetValue(game.SourceId, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"No game management provider is registered for source '{game.SourceId}'.");
    }
}
