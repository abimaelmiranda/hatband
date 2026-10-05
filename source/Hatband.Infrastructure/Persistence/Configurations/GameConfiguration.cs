using Hatband.Core.Models.Games;
using Hatband.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hatband.Infrastructure.Persistence.Configurations;

public sealed class GameConfiguration : ModelBaseConfiguration<Game>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");
        builder.Property(game => game.Name).IsRequired();
        builder.Property(game => game.SourceId).HasConversion<GameSourceIdConverter>();
        builder.HasIndex(game => new { game.SourceId, game.SourceGameId }).IsUnique();

        builder.OwnsOne(game => game.Metadata, metadata =>
        {
            metadata.Property(value => value.LanguageTag).HasColumnName("Metadata_LanguageTag");
            metadata.Property(value => value.StoreName).HasColumnName("Metadata_StoreName");
            metadata.Property(value => value.Description).HasColumnName("Metadata_Description");
            metadata.Property(value => value.Developer).HasColumnName("Metadata_Developer");
            metadata.Property(value => value.Publisher).HasColumnName("Metadata_Publisher");
            metadata.Property(value => value.Genre).HasColumnName("Metadata_Genre");
            metadata.Property(value => value.ReleaseDate).HasColumnName("Metadata_ReleaseDate");
            metadata.Property(value => value.NativePlatforms).HasColumnName("Metadata_NativePlatforms");
        });

        builder.OwnsOne(game => game.Artwork, artwork =>
        {
            artwork.Property(value => value.CoverImagePath).HasColumnName("Artwork_CoverImagePath");
            artwork.Property(value => value.BackgroundImagePath).HasColumnName("Artwork_BackgroundImagePath");
            artwork.Property(value => value.IconPath).HasColumnName("Artwork_IconPath");
        });
        builder.OwnsOne(game => game.DefaultArtwork, artwork =>
        {
            artwork.Property(value => value.CoverImagePath).HasColumnName("DefaultArtwork_CoverImagePath");
            artwork.Property(value => value.BackgroundImagePath).HasColumnName("DefaultArtwork_BackgroundImagePath");
            artwork.Property(value => value.IconPath).HasColumnName("DefaultArtwork_IconPath");
        }).Navigation(game => game.DefaultArtwork).IsRequired(false);
        builder.OwnsOne(game => game.CompatibilityTool, tool =>
        {
            tool.Property(value => value.Name).HasColumnName("CompatibilityTool_Name");
            tool.Property(value => value.Version).HasColumnName("CompatibilityTool_Version");
            tool.Property(value => value.InstallationPath).HasColumnName("CompatibilityTool_InstallationPath");
            tool.Property(value => value.Source).HasColumnName("CompatibilityTool_Source");
        }).Navigation(game => game.CompatibilityTool).IsRequired(false);
        builder.OwnsOne(game => game.InstallationInfo, installation =>
        {
            installation.Property(value => value.InstallDirectory).HasColumnName("InstallationInfo_InstallDirectory");
        }).Navigation(game => game.InstallationInfo).IsRequired(false);
        builder.OwnsOne(game => game.TimeToBeat, timeToBeat =>
        {
            timeToBeat.Property(value => value.HowLongToBeatGameId).HasColumnName("TimeToBeat_GameId");
            timeToBeat.Property(value => value.HowLongToBeatName).HasColumnName("TimeToBeat_GameName");
            timeToBeat.Property(value => value.MainStorySeconds).HasColumnName("TimeToBeat_MainStorySeconds");
            timeToBeat.Property(value => value.MainStoryPlusExtrasSeconds).HasColumnName("TimeToBeat_MainStoryPlusExtrasSeconds");
            timeToBeat.Property(value => value.CompletionistSeconds).HasColumnName("TimeToBeat_CompletionistSeconds");
        }).Navigation(game => game.TimeToBeat).IsRequired(false);
        builder.HasMany(game => game.GameActions).WithOne().OnDelete(DeleteBehavior.Cascade);
    }
}
