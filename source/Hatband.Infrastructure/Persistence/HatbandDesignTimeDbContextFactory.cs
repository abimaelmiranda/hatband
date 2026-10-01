using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hatband.Infrastructure.Persistence;

public sealed class HatbandDesignTimeDbContextFactory : IDesignTimeDbContextFactory<HatbandDbContext>
{
    public HatbandDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HatbandDbContext>()
            .UseSqlite("Data Source=hatband-design-time.db")
            .Options;

        return new HatbandDbContext(options);
    }
}
