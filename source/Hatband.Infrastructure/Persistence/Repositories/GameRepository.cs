using Hatband.Core.Abstractions.Repositories;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence.Repositories;

public sealed class GameRepository(IDbContextFactory<HatbandDbContext> contextFactory) : IGameRepository
{
    public async Task<IReadOnlyList<Game>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Games.AsNoTracking()
            .Include(game => game.GameActions)
            .OrderBy(game => game.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Game?> GetByIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Games.Include(game => game.GameActions)
            .SingleOrDefaultAsync(game => game.Id == gameId, cancellationToken);
    }

    public async Task<IReadOnlyList<Game>> GetBySourceAsync(
        GameSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Games.AsNoTracking().Include(game => game.GameActions)
            .Where(game => game.SourceId == sourceId)
            .OrderBy(game => game.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Game?> GetBySourceIdentityAsync(
        GameSourceId sourceId,
        string sourceGameId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceGameId);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Games.Include(game => game.GameActions)
            .SingleOrDefaultAsync(game => game.SourceId == sourceId && game.SourceGameId == sourceGameId, cancellationToken);
    }

    public async Task AddAsync(Guid libraryId, Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(game.Name);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var library = await context.Libraries.SingleAsync(item => item.Id == libraryId, cancellationToken);
        context.Games.Add(game);
        library.Games.Add(game);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Games.Update(game);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var game = await context.Games.SingleOrDefaultAsync(item => item.Id == gameId, cancellationToken);
        if (game is null)
        {
            return;
        }

        context.Games.Remove(game);
        await context.SaveChangesAsync(cancellationToken);
    }
}
