using Hatband.Core.Models.Libraries;
using Hatband.Core.Abstractions.Games;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<HatbandDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IGameSourceCatalog _gameSourceCatalog;

    public DatabaseInitializer(
        IDbContextFactory<HatbandDbContext> contextFactory,
        TimeProvider timeProvider,
        IGameSourceCatalog gameSourceCatalog)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(gameSourceCatalog);
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
        _gameSourceCatalog = gameSourceCatalog;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await _gameSourceCatalog.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var currentDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        // Accessing the dbContext here is intentional, as ICacheService should not provide a public way to clear the cache.
        await context.CacheEntries
            .Where(entry => entry.ExpiresAt < currentDate)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!await context.Libraries.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            context.Libraries.Add(new GameLibrary());
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
