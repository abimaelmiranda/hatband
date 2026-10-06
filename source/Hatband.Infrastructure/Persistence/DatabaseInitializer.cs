using Hatband.Core.Models.Libraries;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<HatbandDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public DatabaseInitializer(
        IDbContextFactory<HatbandDbContext> contextFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        var currentDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        // Accessing the dbContext here is intentional, as ICacheService should not provide a public way to clear the cache.
        await context.CacheEntries
            .Where(entry => entry.ExpiresAt < currentDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (!await context.Libraries.AnyAsync(cancellationToken))
        {
            context.Libraries.Add(new GameLibrary());
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
