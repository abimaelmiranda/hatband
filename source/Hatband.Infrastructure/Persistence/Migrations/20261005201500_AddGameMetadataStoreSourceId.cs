using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddGameMetadataStoreSourceId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Metadata_StoreSourceId",
            table: "Games",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE Games SET Metadata_StoreSourceId = 'steam' WHERE Metadata_StoreGameId IS NOT NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Metadata_StoreSourceId",
            table: "Games");
    }
}
