using Hatband.Core.Models.Libraries;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence;

public sealed class DatabaseInitializer(IDbContextFactory<HatbandDbContext> contextFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        if (!await context.Libraries.AnyAsync(cancellationToken))
        {
            context.Libraries.Add(new GameLibrary());
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
