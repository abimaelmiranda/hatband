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

    public Task<GameManagementResult> InstallAsync(Game game, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(game, provider => provider.InstallAsync(game, cancellationToken));
    }

    public Task<GameManagementResult> UninstallAsync(Game game, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(game, provider => provider.UninstallAsync(game, cancellationToken));
    }

    public Task<GameManagementResult> LaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(game, provider => provider.LaunchAsync(game, cancellationToken));
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
