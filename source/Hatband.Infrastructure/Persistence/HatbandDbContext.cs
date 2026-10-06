using Hatband.Core.Models;
using Hatband.Core.Models.Games;
using Hatband.Core.Models.Libraries;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence;

public sealed class HatbandDbContext : DbContext
{
    public HatbandDbContext(DbContextOptions<HatbandDbContext> options)
        : base(options)
    {
    }

    public DbSet<GameLibrary> Libraries => Set<GameLibrary>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<CacheEntry> CacheEntries => Set<CacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HatbandDbContext).Assembly);
    }
}
