using Hatband.Core.Models.Libraries;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<HatbandDbContext> _contextFactory;

    public DatabaseInitializer(IDbContextFactory<HatbandDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        if (!await context.Libraries.AnyAsync(cancellationToken))
        {
            context.Libraries.Add(new GameLibrary());
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
