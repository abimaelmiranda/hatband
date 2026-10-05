using Hatband.Core.Models.Libraries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hatband.Infrastructure.Persistence.Configurations;

public sealed class GameLibraryConfiguration : ModelBaseConfiguration<GameLibrary>
{
    protected override void ConfigureEntity(EntityTypeBuilder<GameLibrary> builder)
    {
        builder.ToTable("Libraries");
        builder.Property(item => item.Name).IsRequired();
        builder.HasMany(item => item.Games)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
