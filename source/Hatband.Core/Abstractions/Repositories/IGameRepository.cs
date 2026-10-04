using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;

namespace Hatband.Core.Abstractions.Repositories;

/// <summary>
/// Reads and persists games in the user's library.
/// </summary>
public interface IGameRepository
{
    Task<IReadOnlyList<Game>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Game?> GetByIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Game>> GetBySourceAsync(
        GameSourceId sourceId,
        CancellationToken cancellationToken = default);

    Task<Game?> GetBySourceIdentityAsync(
        GameSourceId sourceId,
        string sourceGameId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Guid libraryId,
        Game game,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(Game game, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid gameId, CancellationToken cancellationToken = default);
}
