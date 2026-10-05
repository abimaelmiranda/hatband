using Hatband.Core.Abstractions.Repositories;
using Hatband.Core.Models.Libraries;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence.Repositories;

public sealed class GameLibraryRepository(IDbContextFactory<HatbandDbContext> contextFactory) : IGameLibraryRepository
{
    public async Task<IReadOnlyList<GameLibrary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Libraries.AsNoTracking().OrderBy(library => library.Name).ToListAsync(cancellationToken);
    }

    public async Task<GameLibrary?> GetByIdAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Libraries.Include(library => library.Games)
            .ThenInclude(game => game.GameActions)
            .SingleOrDefaultAsync(library => library.Id == libraryId, cancellationToken);
    }

    public async Task AddAsync(GameLibrary library, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(library);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Libraries.Add(library);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(GameLibrary library, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(library);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Libraries.Update(library);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var library = await context.Libraries.SingleOrDefaultAsync(item => item.Id == libraryId, cancellationToken);
        if (library is null)
        {
            return;
        }

        context.Libraries.Remove(library);
        await context.SaveChangesAsync(cancellationToken);
    }
}
