using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Host;
using Hatband.Core.Abstractions.Repositories;
using Hatband.Core.Enums.Games;
using Hatband.Core.Models;
using Hatband.Core.Models.Games;

namespace Hatband.Infrastructure.Services.Games;

public sealed class GameManagementService : IGameManagementService
{
    private readonly IReadOnlyDictionary<string, IGameManagementProvider> _providers;
    private readonly IGameLaunchRuntimeRegistry _runtimeRegistry;
    private readonly IHostSystemInfo _hostSystemInfo;
    private readonly IReadOnlyDictionary<string, IGamePostInstallProvider> _postInstallProviders;
    private readonly IGameRepository _gameRepository;

    public GameManagementService(
        IEnumerable<IGameManagementProvider> providers,
        IGameLaunchRuntimeRegistry runtimeRegistry,
        IHostSystemInfo hostSystemInfo,
        IEnumerable<IGamePostInstallProvider> postInstallProviders,
        IGameRepository gameRepository)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(runtimeRegistry);
        ArgumentNullException.ThrowIfNull(hostSystemInfo);
        ArgumentNullException.ThrowIfNull(postInstallProviders);
        ArgumentNullException.ThrowIfNull(gameRepository);
        _providers = providers.ToDictionary(provider => provider.SourceId);
        _runtimeRegistry = runtimeRegistry;
        _hostSystemInfo = hostSystemInfo;
        _postInstallProviders = postInstallProviders.ToDictionary(provider => provider.SourceId);
        _gameRepository = gameRepository;
    }

    public Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(Game game, CancellationToken cancellationToken = default) =>
        GetProvider(game).GetInstallLocationsAsync(game, cancellationToken);

    public Task<GameManagementResult> InstallAsync(Game game, GameInstallLocation? location = null, CancellationToken cancellationToken = default) =>
        GetProvider(game).InstallAsync(game, location, cancellationToken);

    public Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default) =>
        GetProvider(game).UninstallAsync(game, cancellationToken);

    public bool SupportsLaunchOptions(Game game) => GetProvider(game).SupportsLaunchOptions;

    public Task<IReadOnlyList<GameLaunchOption>> GetLaunchOptionsAsync(Game game, CancellationToken cancellationToken = default) =>
        GetProvider(game).GetLaunchOptionsAsync(game, cancellationToken);

    public async Task<GameLaunchResult> LaunchAsync(
        Game game,
        IProgress<GamePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var provider = GetProvider(game);
        var executionMode = game.CompatibilityLayer?.ExecutionMode ?? GameExecutionMode.Original;
        var context = new GameLaunchContext(_hostSystemInfo.Platform, executionMode);
        var plan = await provider.PrepareLaunchAsync(game, context, cancellationToken);
        if (plan is DelegatedGameLaunchPlan)
        {
            return await provider.LaunchOriginalAsync(game, cancellationToken);
        }

        if (plan is not ExecutableGameLaunchPlan executablePlan)
        {
            throw new InvalidOperationException($"The launch provider for '{game.Name}' returned an unsupported launch plan.");
        }

        var layer = game.CompatibilityLayer;
        CompatibilityTool? configuredTool = layer?.Tool;
        IReadOnlyDictionary<string, string> environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal);
        string? additionalArguments = null;
        if (executionMode == GameExecutionMode.Custom)
        {
            var customLayer = layer
                ?? throw new InvalidOperationException($"Custom compatibility is enabled for '{game.Name}', but its configuration is missing.");
            environmentVariables = customLayer.EnvironmentVariables;
            additionalArguments = customLayer.AdditionalArguments;
        }

        GameLaunchEnvironmentPolicy.ValidateUserVariables(environmentVariables);

        var runtime = _runtimeRegistry.Resolve(
            context.HostOperatingSystem,
            executablePlan.TargetOperatingSystem,
            executablePlan.RuntimeId);
        var tool = runtime.RequiresCompatibilityTool
            ? configuredTool ?? throw new InvalidOperationException(
                $"The selected {runtime.RuntimeId} runtime requires a configured compatibility tool.")
            : null;
        var installDirectory = GetInstallDirectory(game, executablePlan.Command);
        await RefreshPostInstallPlanAsync(game, cancellationToken);
        if (game.PostInstallPlan is not null && !runtime.SupportsPostInstall)
        {
            throw new InvalidOperationException(
                $"The selected runtime '{runtime.RuntimeId}' does not support the configured post-install plan for '{game.Name}'.");
        }

        var process = await runtime.LaunchAsync(
            new GameRuntimeLaunchRequest(
                game.Id,
                tool,
                executablePlan.Command,
                additionalArguments,
                environmentVariables,
                installDirectory,
                runtime.SupportsPostInstall ? game.PostInstallPlan : null,
                progress),
            cancellationToken);
        if (process is null)
        {
            return new GameLaunchResult(GameManagementResult.Unavailable);
        }

        var watchTarget = new GameProcessWatchTarget(
            installDirectory,
            new GameRuntimeProcessWatchTarget(
                game.Id,
                executablePlan.Command.ExecutablePath,
                process.Completion,
                runtime.ProcessWatchMode));
        return new GameLaunchResult(GameManagementResult.GameActionStarted, watchTarget);
    }

    private static string GetInstallDirectory(Game game, GameLaunchCommand command)
    {
        var installDirectory = game.InstallationInfo?.InstallDirectory;
        if (!string.IsNullOrWhiteSpace(installDirectory))
        {
            return installDirectory;
        }

        var executableDirectory = Path.GetDirectoryName(command.ExecutablePath);
        if (!string.IsNullOrWhiteSpace(executableDirectory))
        {
            return executableDirectory;
        }

        throw new InvalidOperationException($"No install directory can be determined for '{game.Name}'.");
    }

    private async Task RefreshPostInstallPlanAsync(Game game, CancellationToken cancellationToken)
    {
        if (!_postInstallProviders.TryGetValue(game.SourceId, out var provider))
        {
            return;
        }

        var plan = await provider.GetPlanAsync(game, cancellationToken);
        if (plan?.Revision == game.PostInstallPlan?.Revision)
        {
            return;
        }

        await _gameRepository.UpdatePostInstallPlanAsync(game.Id, plan, cancellationToken);
        game.PostInstallPlan = plan;
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
