using Hatband.Core.Models.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hatband.Infrastructure.Persistence.Configurations;

public sealed class GamePluginDataConfiguration : ModelBaseConfiguration<GamePluginData>
{
    protected override void ConfigureEntity(EntityTypeBuilder<GamePluginData> builder)
    {
        builder.ToTable("GamePluginData");
        builder.Property(data => data.PluginId).IsRequired();
        builder.Property(data => data.DataKey).IsRequired();
        builder.Property(data => data.JsonPayload).IsRequired();
        builder.HasIndex(data => new { data.GameId, data.PluginId, data.DataKey }).IsUnique();
        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(data => data.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
