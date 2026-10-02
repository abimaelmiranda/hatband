using Hatband.Core.Abstractions;

namespace Hatband.Infrastructure.Services;

public sealed class GameInstallationStateSyncService : IGameInstallationStateSyncService
{
    private readonly IGameLibraryService gameLibraryService;
    private readonly IReadOnlyList<IGameInstallationProvider> providers;

    public GameInstallationStateSyncService(
        IGameLibraryService gameLibraryService,
        IEnumerable<IGameInstallationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(gameLibraryService);
        ArgumentNullException.ThrowIfNull(providers);
        this.gameLibraryService = gameLibraryService;
        this.providers = providers.ToArray();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var installedGames = await provider.ScanInstalledGamesAsync(cancellationToken);
            await gameLibraryService.RefreshInstallationStatesAsync(
                provider.SourceId,
                installedGames,
                cancellationToken);
        }
    }
}
