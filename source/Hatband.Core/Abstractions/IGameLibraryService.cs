using Hatband.Core.Enums.Stores;
using Hatband.Core.Models;

namespace Hatband.Core.Abstractions;

public interface IGameLibraryService
{
    Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken cancellationToken = default);

    Task AddGameAsync(Game game, CancellationToken cancellationToken = default);

    Task UpdateGameMetadataAsync(
        Guid gameId,
        GameMetadata metadata,
        CancellationToken cancellationToken = default);

    Task UpdateGameDetailsAsync(
        Guid gameId,
        string name,
        bool isNameCustomized,
        GameMetadata metadata,
        CancellationToken cancellationToken = default);

    Task UpdateGameTimeToBeatAsync(
        Guid gameId,
        GameTimeToBeat? timeToBeat,
        CancellationToken cancellationToken = default);

    Task SetGameHiddenAsync(
        Guid gameId,
        bool isHidden,
        CancellationToken cancellationToken = default);

    Task SynchronizeGamesAsync(
        GameSourceId sourceId,
        IReadOnlyList<Game> importedGames,
        CancellationToken cancellationToken = default);
}
