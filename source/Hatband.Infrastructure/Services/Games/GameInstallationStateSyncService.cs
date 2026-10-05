using Hatband.Core.Abstractions.Repositories;
using Hatband.Core.Models.Games;
using Hatband.Core.Models.Libraries;

namespace Hatband.Infrastructure.Services.Games;

public sealed class GameInstallationStateSyncService : IGameInstallationStateSyncService
{
    private readonly IGameRepository _gameRepository;
    private readonly IGameLibraryRepository _libraryRepository;
    private readonly IReadOnlyList<IGameInstallationProvider> _providers;

    public GameInstallationStateSyncService(
        IGameRepository gameRepository,
        IGameLibraryRepository libraryRepository,
        IEnumerable<IGameInstallationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(gameRepository);
        ArgumentNullException.ThrowIfNull(libraryRepository);
        ArgumentNullException.ThrowIfNull(providers);
        _gameRepository = gameRepository;
        _libraryRepository = libraryRepository;
        _providers = providers.ToArray();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        GameLibrary? defaultLibrary = null;
        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var installedGames = await provider.ScanInstalledGamesAsync(cancellationToken);
            var games = await _gameRepository.GetBySourceAsync(provider.SourceId, cancellationToken);
            var installedGamesBySourceId = installedGames.ToDictionary(
                game => game.SourceGameId ?? throw new InvalidOperationException(
                    $"Installation provider {provider.SourceId} returned a game without a source ID."),
                StringComparer.Ordinal);
            var knownSourceGameIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var game in games)
            {
                if (game.SourceGameId is { } sourceGameId)
                {
                    knownSourceGameIds.Add(sourceGameId);
                }
            }

            foreach (var game in games)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var installedGame = game.SourceGameId is { } sourceGameId &&
                    installedGamesBySourceId.TryGetValue(sourceGameId, out var foundGame)
                    ? foundGame
                    : null;
                if (game.InstallationInfo == installedGame?.InstallationInfo)
                {
                    continue;
                }

                await _gameRepository.UpdateInstallationInfoAsync(
                    game.Id,
                    installedGame?.InstallationInfo,
                    cancellationToken);
            }

            foreach (var (sourceGameId, installedGame) in installedGamesBySourceId)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (knownSourceGameIds.Contains(sourceGameId))
                {
                    continue;
                }

                if (defaultLibrary is null)
                {
                    defaultLibrary = (await _libraryRepository.GetAllAsync(cancellationToken)).FirstOrDefault();
                    if (defaultLibrary is null)
                    {
                        defaultLibrary = new GameLibrary();
                        await _libraryRepository.AddAsync(defaultLibrary, cancellationToken);
                    }
                }

                await _gameRepository.AddAsync(defaultLibrary.Id, installedGame, cancellationToken);
                knownSourceGameIds.Add(sourceGameId);
            }
        }
    }
}
