using Hatband.Core.Models.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hatband.Infrastructure.Persistence.Configurations;

public sealed class GameActionConfiguration : ModelBaseConfiguration<GameAction>
{
    protected override void ConfigureEntity(EntityTypeBuilder<GameAction> builder)
    {
        builder.ToTable("GameActions");
        builder.Property(action => action.Name).IsRequired();
        builder.Property(action => action.Target).IsRequired();
    }
}
