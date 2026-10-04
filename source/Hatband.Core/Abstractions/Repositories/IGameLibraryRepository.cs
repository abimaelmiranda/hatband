using Hatband.Core.Models.Libraries;

namespace Hatband.Core.Abstractions.Repositories;

/// <summary>
/// Reads and persists game libraries.
/// </summary>
public interface IGameLibraryRepository
{
    Task<IReadOnlyList<GameLibrary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<GameLibrary?> GetByIdAsync(Guid libraryId, CancellationToken cancellationToken = default);

    Task AddAsync(GameLibrary library, CancellationToken cancellationToken = default);

    Task UpdateAsync(GameLibrary library, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid libraryId, CancellationToken cancellationToken = default);
}
