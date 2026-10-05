using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hatband.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HatbandDbContext))]
[Migration(MigrationId)]
public sealed partial class AdaptGameModelsToCore : Migration
{
    public const string MigrationId = "20261004000000_AdaptGameModelsToCore";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE Games SET SourceId = 'manual' WHERE SourceId IS NULL;");
        migrationBuilder.AlterColumn<string>(
            name: "SourceId", table: "Games", type: "TEXT", nullable: false,
            oldClrType: typeof(string), oldType: "TEXT", oldNullable: true);

        migrationBuilder.RenameColumn("Genre", "Games", "Metadata_Genre");
        migrationBuilder.RenameColumn("Metadata_Artwork_CoverImagePath", "Games", "Artwork_CoverImagePath");
        migrationBuilder.RenameColumn("Metadata_Artwork_BackgroundImagePath", "Games", "Artwork_BackgroundImagePath");
        migrationBuilder.RenameColumn("Metadata_Artwork_IconPath", "Games", "Artwork_IconPath");
        migrationBuilder.RenameColumn("InstallDirectory", "Games", "InstallationInfo_InstallDirectory");

        migrationBuilder.AddColumn<string>("DefaultArtwork_CoverImagePath", "Games", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>("DefaultArtwork_BackgroundImagePath", "Games", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>("DefaultArtwork_IconPath", "Games", type: "TEXT", nullable: true);
        migrationBuilder.Sql(
            "UPDATE Games SET " +
            "DefaultArtwork_CoverImagePath = CASE WHEN Metadata_Artwork_IsCoverCustomized = 0 THEN Artwork_CoverImagePath END, " +
            "DefaultArtwork_BackgroundImagePath = CASE WHEN Metadata_Artwork_IsBackgroundCustomized = 0 THEN Artwork_BackgroundImagePath END, " +
            "DefaultArtwork_IconPath = CASE WHEN Metadata_Artwork_IsIconCustomized = 0 THEN Artwork_IconPath END;");

        migrationBuilder.DropColumn("IsInstalled", "Games");
        migrationBuilder.DropColumn("IsNameCustomized", "Games");
        migrationBuilder.DropColumn("Metadata_Artwork_IsCoverCustomized", "Games");
        migrationBuilder.DropColumn("Metadata_Artwork_IsBackgroundCustomized", "Games");
        migrationBuilder.DropColumn("Metadata_Artwork_IsIconCustomized", "Games");
        migrationBuilder.DropColumn("Metadata_Overrides_Description", "Games");
        migrationBuilder.DropColumn("Metadata_Overrides_Developer", "Games");
        migrationBuilder.DropColumn("Metadata_Overrides_Publisher", "Games");
        migrationBuilder.DropColumn("Metadata_Overrides_Genre", "Games");
        migrationBuilder.DropColumn("Metadata_Overrides_ReleaseDate", "Games");
        migrationBuilder.DropColumn("TimeToBeat_LastSearchedAtUtc", "Games");

        migrationBuilder.AddColumn<string>("CompatibilityTool_Name", "Games", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>("CompatibilityTool_Version", "Games", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>("CompatibilityTool_InstallationPath", "Games", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>("CompatibilityTool_Source", "Games", type: "INTEGER", nullable: true);

        migrationBuilder.RenameTable(name: "GameLaunchActions", newName: "GameActions");
        migrationBuilder.RenameIndex(
            name: "IX_GameLaunchActions_GameId",
            table: "GameActions",
            newName: "IX_GameActions_GameId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(name: "GameActions", newName: "GameLaunchActions");
        migrationBuilder.RenameIndex(
            name: "IX_GameActions_GameId",
            table: "GameLaunchActions",
            newName: "IX_GameLaunchActions_GameId");
        migrationBuilder.AddColumn<bool>("IsInstalled", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("IsNameCustomized", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("Metadata_Artwork_IsCoverCustomized", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("Metadata_Artwork_IsBackgroundCustomized", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("Metadata_Artwork_IsIconCustomized", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("Metadata_Overrides_Description", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("Metadata_Overrides_Developer", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("Metadata_Overrides_Publisher", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("Metadata_Overrides_Genre", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("Metadata_Overrides_ReleaseDate", "Games", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTime>("TimeToBeat_LastSearchedAtUtc", "Games", type: "TEXT", nullable: true);
        migrationBuilder.DropColumn("DefaultArtwork_CoverImagePath", "Games");
        migrationBuilder.DropColumn("DefaultArtwork_BackgroundImagePath", "Games");
        migrationBuilder.DropColumn("DefaultArtwork_IconPath", "Games");
        migrationBuilder.DropColumn("CompatibilityTool_Name", "Games");
        migrationBuilder.DropColumn("CompatibilityTool_Version", "Games");
        migrationBuilder.DropColumn("CompatibilityTool_InstallationPath", "Games");
        migrationBuilder.DropColumn("CompatibilityTool_Source", "Games");
        migrationBuilder.RenameColumn("Metadata_Genre", "Games", "Genre");
        migrationBuilder.RenameColumn("Artwork_CoverImagePath", "Games", "Metadata_Artwork_CoverImagePath");
        migrationBuilder.RenameColumn("Artwork_BackgroundImagePath", "Games", "Metadata_Artwork_BackgroundImagePath");
        migrationBuilder.RenameColumn("Artwork_IconPath", "Games", "Metadata_Artwork_IconPath");
        migrationBuilder.RenameColumn("InstallationInfo_InstallDirectory", "Games", "InstallDirectory");
        migrationBuilder.AlterColumn<string>(
            name: "SourceId", table: "Games", type: "TEXT", nullable: true,
            oldClrType: typeof(string), oldType: "TEXT");
    }
}
