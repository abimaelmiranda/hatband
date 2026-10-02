using Hatband.Core.Abstractions;
using Hatband.Core.Enums;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Infrastructure.Services;

public sealed class GameManagementService : IGameManagementService
{
    private readonly IReadOnlyDictionary<GameSourceId, IGameManagementProvider> providersBySource;

    public GameManagementService(IEnumerable<IGameManagementProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        providersBySource = providers.ToDictionary(provider => provider.SourceId);
    }

    public Task<IReadOnlyList<GameInstallLocation>> GetInstallLocationsAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.SourceId is not GameSourceId sourceId || !providersBySource.TryGetValue(sourceId, out var provider))
        {
            return Task.FromResult<IReadOnlyList<GameInstallLocation>>([]);
        }

        return provider.GetInstallLocationsAsync(game, cancellationToken);
    }

    public Task<GameManagementResult> InstallAsync(
        Game game,
        GameInstallLocation? location = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(game, provider => provider.InstallAsync(game, location, cancellationToken));
    }

    public Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(game, provider => provider.UninstallAsync(game, cancellationToken));
    }

    public Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(game, provider => provider.LaunchAsync(game, cancellationToken));
    }

    public GameProcessWatchTarget? GetProcessWatchTarget(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.SourceId is not GameSourceId sourceId ||
            !providersBySource.TryGetValue(sourceId, out var provider))
        {
            return null;
        }

        return provider.GetProcessWatchTarget(game);
    }

    private Task<GameManagementResult> ExecuteAsync(
        Game game,
        Func<IGameManagementProvider, Task<GameManagementResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(operation);

        if (game.SourceId is not GameSourceId sourceId ||
            !providersBySource.TryGetValue(sourceId, out var provider))
        {
            return Task.FromResult(GameManagementResult.Unsupported);
        }

        return operation(provider);
    }
}
