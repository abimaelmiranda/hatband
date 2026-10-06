using Hatband.Core.Abstractions.Repositories;
using Hatband.Core.Abstractions.FileSystem;
using Hatband.Core.Enums.Stores;
using Hatband.Core.Models.Games;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence.Repositories;

public sealed class GameRepository : IGameRepository
{
    private readonly IDbContextFactory<HatbandDbContext> _contextFactory;
    private readonly IAppDataFileSystem _appDataFileSystem;

    public GameRepository(
        IDbContextFactory<HatbandDbContext> contextFactory,
        IAppDataFileSystem appDataFileSystem)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(appDataFileSystem);
        _contextFactory = contextFactory;
        _appDataFileSystem = appDataFileSystem;
    }

    public async Task<IReadOnlyList<Game>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Games.AsNoTracking()
            .Include(game => game.GameActions)
            .OrderBy(game => game.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Game?> GetByIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Games.Include(game => game.GameActions)
            .SingleOrDefaultAsync(game => game.Id == gameId, cancellationToken);
    }

    public async Task<IReadOnlyList<Game>> GetBySourceAsync(
        GameSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
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
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Games.Include(game => game.GameActions)
            .SingleOrDefaultAsync(game => game.SourceId == sourceId && game.SourceGameId == sourceGameId, cancellationToken);
    }

    public async Task AddAsync(Guid libraryId, Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(game.Name);
        _appDataFileSystem.CreateDirectory(Path.Combine("games", game.Id.ToString("D"), "artwork"));
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var library = await context.Libraries.SingleAsync(item => item.Id == libraryId, cancellationToken);
        context.Games.Add(game);
        library.Games.Add(game);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Games.Update(game);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateInstallationInfoAsync(
        Guid gameId,
        GameInstallationInfo? installationInfo,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var game = await context.Games.SingleAsync(item => item.Id == gameId, cancellationToken);
        game.InstallationInfo = installationInfo;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var game = await context.Games.SingleOrDefaultAsync(item => item.Id == gameId, cancellationToken);
        if (game is null)
        {
            return;
        }

        context.Games.Remove(game);
        await context.SaveChangesAsync(cancellationToken);
    }
}
