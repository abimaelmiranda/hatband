using Hatband.Core.Models;
using Hatband.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hatband.Infrastructure.Persistence.Configurations;

public sealed class GameConfiguration : ModelBaseConfiguration<Game>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");
        builder.Property(item => item.Name).IsRequired();
        builder.Property(item => item.IsNameCustomized).HasDefaultValue(false);
        builder.Property(item => item.SourceId).HasConversion<GameSourceIdConverter>();
        builder.HasIndex(item => new { item.SourceId, item.SourceGameId }).IsUnique();
        builder.OwnsOne(item => item.Metadata, metadata =>
        {
            metadata.Property(item => item.LanguageTag).HasColumnName("Metadata_LanguageTag");
            metadata.Property(item => item.StoreName).HasColumnName("Metadata_StoreName");
            metadata.Property(item => item.Genre).HasColumnName("Genre");
            metadata.OwnsOne(item => item.Artwork, artwork =>
            {
                artwork.Property(item => item.IsCoverCustomized)
                    .HasColumnName("Metadata_Artwork_IsCoverCustomized")
                    .HasDefaultValue(false);
                artwork.Property(item => item.IsBackgroundCustomized)
                    .HasColumnName("Metadata_Artwork_IsBackgroundCustomized")
                    .HasDefaultValue(false);
                artwork.Property(item => item.IsIconCustomized)
                    .HasColumnName("Metadata_Artwork_IsIconCustomized")
                    .HasDefaultValue(false);
            });
            metadata.OwnsOne(item => item.Overrides, overrides =>
            {
                overrides.Property(item => item.Description)
                    .HasColumnName("Metadata_Overrides_Description")
                    .HasDefaultValue(false);
                overrides.Property(item => item.Developer)
                    .HasColumnName("Metadata_Overrides_Developer")
                    .HasDefaultValue(false);
                overrides.Property(item => item.Publisher)
                    .HasColumnName("Metadata_Overrides_Publisher")
                    .HasDefaultValue(false);
                overrides.Property(item => item.Genre)
                    .HasColumnName("Metadata_Overrides_Genre")
                    .HasDefaultValue(false);
                overrides.Property(item => item.ReleaseDate)
                    .HasColumnName("Metadata_Overrides_ReleaseDate")
                    .HasDefaultValue(false);
            });
        });
        builder.OwnsOne(item => item.TimeToBeat, timeToBeat =>
        {
            timeToBeat.Property(item => item.HowLongToBeatGameId).HasColumnName("TimeToBeat_GameId");
            timeToBeat.Property(item => item.HowLongToBeatName).HasColumnName("TimeToBeat_GameName");
            timeToBeat.Property(item => item.MainStorySeconds).HasColumnName("TimeToBeat_MainStorySeconds");
            timeToBeat.Property(item => item.MainStoryPlusExtrasSeconds).HasColumnName("TimeToBeat_MainStoryPlusExtrasSeconds");
            timeToBeat.Property(item => item.CompletionistSeconds).HasColumnName("TimeToBeat_CompletionistSeconds");
            timeToBeat.Property(item => item.LastSearchedAtUtc).HasColumnName("TimeToBeat_LastSearchedAtUtc");
        });
        builder.Navigation(item => item.TimeToBeat).IsRequired(false);
        builder.HasMany(item => item.LaunchActions)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
