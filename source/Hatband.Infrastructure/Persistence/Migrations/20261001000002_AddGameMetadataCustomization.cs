using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hatband.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HatbandDbContext))]
[Migration(MigrationId)]
public sealed class AddGameMetadataCustomization : Migration
{
    public const string MigrationId = "20261001000002_AddGameMetadataCustomization";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsNameCustomized",
            table: "Games",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Metadata_LanguageTag",
            table: "Games",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Metadata_StoreName",
            table: "Games",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "Metadata_Artwork_IsCoverCustomized",
            table: "Games",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Metadata_Artwork_IsBackgroundCustomized",
            table: "Games",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Metadata_Artwork_IsIconCustomized",
            table: "Games",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Metadata_Overrides_Description",
            table: "Games",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Metadata_Overrides_Developer",
            table: "Games",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Metadata_Overrides_Publisher",
            table: "Games",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Metadata_Overrides_Genre",
            table: "Games",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Metadata_Overrides_ReleaseDate",
            table: "Games",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsNameCustomized", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_LanguageTag", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_StoreName", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_Artwork_IsCoverCustomized", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_Artwork_IsBackgroundCustomized", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_Artwork_IsIconCustomized", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_Overrides_Description", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_Overrides_Developer", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_Overrides_Publisher", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_Overrides_Genre", table: "Games");
        migrationBuilder.DropColumn(name: "Metadata_Overrides_ReleaseDate", table: "Games");
    }
}
