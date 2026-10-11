using Hatband.Core.Models.Games;
using Hatband.Core.Models.Games.PostInstall;
using Hatband.Infrastructure.Persistence.Comparers;
using Hatband.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Hatband.Infrastructure.Persistence.Configurations;

public sealed class GameConfiguration : ModelBaseConfiguration<Game>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");
        builder.Property(game => game.Name).IsRequired();
        builder.Property(game => game.SourceId).HasConversion<GameSourceIdConverter>();
        builder
            .HasIndex(game => new { game.SourceId, game.SourceGameId })
            .IsUnique();

        builder.OwnsOne(
            game => game.Metadata,
            metadata =>
            {
                metadata.Property(value => value.LanguageTag).HasColumnName("Metadata_LanguageTag");
                metadata.Property(value => value.StoreName).HasColumnName("Metadata_StoreName");
                metadata.Property(value => value.StoreSourceId)
                    .HasConversion<GameSourceIdConverter>()
                    .HasColumnName("Metadata_StoreSourceId");
                metadata.Property(value => value.StoreGameId).HasColumnName("Metadata_StoreGameId");
                metadata.Property(value => value.Description).HasColumnName("Metadata_Description");
                metadata.Property(value => value.Developer).HasColumnName("Metadata_Developer");
                metadata.Property(value => value.Publisher).HasColumnName("Metadata_Publisher");
                metadata.Property(value => value.Genre).HasColumnName("Metadata_Genre");
                metadata.Property(value => value.ReleaseDate).HasColumnName("Metadata_ReleaseDate");
                metadata.Property(value => value.NativePlatforms).HasColumnName("Metadata_NativePlatforms");
            });

        builder.ComplexProperty(
            game => game.Artwork,
            artwork =>
            {
                artwork.Property(value => value.CoverImagePath).HasColumnName("Artwork_CoverImagePath");
                artwork.Property(value => value.BackgroundImagePath).HasColumnName("Artwork_BackgroundImagePath");
                artwork.Property(value => value.IconPath).HasColumnName("Artwork_IconPath");
            });
        builder.ComplexProperty(
            game => game.DefaultArtwork,
            artwork =>
            {
                artwork.HasDiscriminator();
                artwork.Property(value => value.CoverImagePath).HasColumnName("DefaultArtwork_CoverImagePath");
                artwork.Property(value => value.BackgroundImagePath).HasColumnName("DefaultArtwork_BackgroundImagePath");
                artwork.Property(value => value.IconPath).HasColumnName("DefaultArtwork_IconPath");
            });

        builder.ComplexProperty(
            game => game.PostInstallPlan,
            plan =>
            {
                plan.HasDiscriminator();
                plan.Property(value => value.Revision).HasColumnName("PostInstallPlan_Revision");

                var steps = plan.Property(value => value.Steps)
                    .HasColumnName("PostInstallPlan_Steps")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value),
                        value => GamePostInstallListComparer.DeserializeSteps(value));
                steps.Metadata.SetValueComparer(GamePostInstallListComparer.Steps);

                var diagnostics = plan.Property(value => value.Diagnostics)
                    .HasColumnName("PostInstallPlan_Diagnostics")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value),
                        value => GamePostInstallListComparer.DeserializeDiagnostics(value));
                diagnostics.Metadata.SetValueComparer(GamePostInstallListComparer.Diagnostics);
            });

        builder.ComplexProperty(
            game => game.CompatibilityLayer,
            layer =>
            {
                layer.HasDiscriminator();
                layer.Property(value => value.ExecutionMode)
                    .HasColumnName("CompatibilityLayer_ExecutionMode");
                layer.Property(value => value.Tier).HasColumnName("CompatibilityLayer_Tier");
                layer.Property(value => value.LaunchOptionId).HasColumnName("CompatibilityLayer_LaunchOptionId");
                layer.Property(value => value.ExecutablePath).HasColumnName("CompatibilityLayer_ExecutablePath");
                layer.Property(value => value.WorkingDirectory).HasColumnName("CompatibilityLayer_WorkingDirectory");
                layer.Property(value => value.LaunchArguments).HasColumnName("CompatibilityLayer_LaunchArguments");
                layer.Property(value => value.AdditionalArguments).HasColumnName("CompatibilityLayer_AdditionalArguments");
                var environmentVariables = layer.Property(value => value.EnvironmentVariables)
                    .HasColumnName("CompatibilityLayer_EnvironmentVariables")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value),
                        value => DeserializeEnvironmentVariables(value));
                environmentVariables.Metadata.SetValueComparer(new EnvironmentVariablesComparer());
                layer.ComplexProperty(
                    value => value.Tool,
                    tool =>
                    {
                        tool.HasDiscriminator();
                        tool.Property(value => value.Name).HasColumnName("CompatibilityTool_Name");
                        tool.Property(value => value.Version).HasColumnName("CompatibilityTool_Version");
                        tool.Property(value => value.InstallationPath)
                            .HasColumnName("CompatibilityTool_InstallationPath");
                        tool.Property(value => value.Source).HasColumnName("CompatibilityTool_Source");
                    });
            });

        builder.OwnsOne(
            game => game.InstallationInfo,
            installation =>
            {
                installation.Property(value => value.InstallDirectory)
                    .HasColumnName("InstallationInfo_InstallDirectory");
            })
            .Navigation(game => game.InstallationInfo)
            .IsRequired(false);
        builder.OwnsOne(
            game => game.TimeToBeat,
            timeToBeat =>
            {
                timeToBeat.Property(value => value.HowLongToBeatGameId).HasColumnName("TimeToBeat_GameId");
                timeToBeat.Property(value => value.HowLongToBeatName).HasColumnName("TimeToBeat_GameName");
                timeToBeat.Property(value => value.MainStorySeconds).HasColumnName("TimeToBeat_MainStorySeconds");
                timeToBeat.Property(value => value.MainStoryPlusExtrasSeconds)
                    .HasColumnName("TimeToBeat_MainStoryPlusExtrasSeconds");
                timeToBeat.Property(value => value.CompletionistSeconds)
                    .HasColumnName("TimeToBeat_CompletionistSeconds");
            })
            .Navigation(game => game.TimeToBeat)
            .IsRequired(false);

        builder.HasMany(game => game.GameActions)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static Dictionary<string, string> DeserializeEnvironmentVariables(string value)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(value)
            ?? throw new InvalidOperationException("The saved game environment is invalid.");
    }
}
