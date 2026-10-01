using Hatband.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Hatband.Infrastructure.Persistence;

public sealed class HatbandDbContext(DbContextOptions<HatbandDbContext> options) : DbContext(options)
{
    public DbSet<GameLibrary> Libraries => Set<GameLibrary>();

    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HatbandDbContext).Assembly);
    }
}
