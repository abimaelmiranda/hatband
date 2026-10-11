using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HatbandDbContext))]
[Migration("20261011193000_AddPluginGameData")]
public sealed partial class AddPluginGameData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GamePluginData",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                PluginId = table.Column<string>(type: "TEXT", nullable: false),
                DataKey = table.Column<string>(type: "TEXT", nullable: false),
                SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                JsonPayload = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GamePluginData", row => row.Id);
                table.ForeignKey(
                    name: "FK_GamePluginData_Games_GameId",
                    column: row => row.GameId,
                    principalTable: "Games",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GamePluginData_GameId_PluginId_DataKey",
            table: "GamePluginData",
            columns: new[] { "GameId", "PluginId", "DataKey" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GamePluginData");
    }
}
