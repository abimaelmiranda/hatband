namespace Hatband.Infrastructure.Services.Games;

public sealed class GameInstallationStateSyncService : IGameInstallationStateSyncService
{
    private readonly IGameRepository gameRepository;
    private readonly IReadOnlyList<IGameInstallationProvider> providers;

    public GameInstallationStateSyncService(
        IGameRepository gameRepository,
        IEnumerable<IGameInstallationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(gameRepository);
        ArgumentNullException.ThrowIfNull(providers);
        this.gameRepository = gameRepository;
        this.providers = providers.ToArray();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var installedGames = await provider.ScanInstalledGamesAsync(cancellationToken);
            var games = await gameRepository.GetBySourceAsync(provider.SourceId, cancellationToken);
            foreach (var game in games)
            {
                cancellationToken.ThrowIfCancellationRequested();
                game.InstallationInfo = game.SourceGameId is { } sourceGameId &&
                    installedGames.TryGetValue(sourceGameId, out var installation)
                    ? installation
                    : null;
                await gameRepository.UpdateAsync(game, cancellationToken);
            }
        }
    }
}
