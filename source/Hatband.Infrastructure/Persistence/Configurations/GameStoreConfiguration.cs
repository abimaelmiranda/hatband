using Hatband.Core.Models.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hatband.Infrastructure.Persistence.Configurations;

public sealed class GameStoreConfiguration : IEntityTypeConfiguration<GameStore>
{
    public void Configure(EntityTypeBuilder<GameStore> builder)
    {
        builder.ToTable("GameStores");
        builder.HasKey(store => store.Id);
        builder.Property(store => store.Id).ValueGeneratedOnAdd();
        builder.Property(store => store.SourceId).IsRequired();
        builder.Property(store => store.Name).IsRequired();
        builder.HasIndex(store => store.SourceId).IsUnique();
    }
}
