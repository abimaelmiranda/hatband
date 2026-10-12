using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameStores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LogoPath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameStores", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "GameStores",
                columns: new[] { "Id", "Name", "SourceId" },
                values: new object[] { 1, "Manual", "manual" });

            migrationBuilder.Sql(
                "INSERT INTO GameStores (SourceId, Name) " +
                "SELECT DISTINCT SourceId, SourceId FROM Games WHERE SourceId <> 'manual';");

            migrationBuilder.AddColumn<int>(
                name: "GameStoreId",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                "UPDATE Games SET GameStoreId = " +
                "(SELECT Id FROM GameStores WHERE GameStores.SourceId = Games.SourceId);");

            migrationBuilder.CreateIndex(
                name: "IX_Games_GameStoreId",
                table: "Games",
                column: "GameStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_GameStores_SourceId",
                table: "GameStores",
                column: "SourceId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_GameStores_GameStoreId",
                table: "Games",
                column: "GameStoreId",
                principalTable: "GameStores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_GameStores_GameStoreId",
                table: "Games");

            migrationBuilder.DropTable(
                name: "GameStores");

            migrationBuilder.DropIndex(
                name: "IX_Games_GameStoreId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "GameStoreId",
                table: "Games");
        }
    }
}
