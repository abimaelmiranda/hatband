using Hatband.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hatband.Infrastructure.Persistence.Configurations;

public sealed class GameLaunchActionConfiguration : ModelBaseConfiguration<GameLaunchAction>
{
    protected override void ConfigureEntity(EntityTypeBuilder<GameLaunchAction> builder)
    {
        builder.ToTable("GameLaunchActions");
    }
}
