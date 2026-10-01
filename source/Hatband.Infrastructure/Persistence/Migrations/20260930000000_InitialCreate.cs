using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hatband.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HatbandDbContext))]
[Migration(MigrationId)]
public sealed class InitialCreate : Migration
{
    public const string MigrationId = "20260930000000_InitialCreate";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Libraries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Libraries", item => item.Id));

        migrationBuilder.CreateTable(
            name: "Games",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                SourceId = table.Column<string>(type: "TEXT", nullable: true),
                SourceGameId = table.Column<string>(type: "TEXT", nullable: true),
                Metadata_Description = table.Column<string>(type: "TEXT", nullable: true),
                Metadata_Developer = table.Column<string>(type: "TEXT", nullable: true),
                Metadata_Publisher = table.Column<string>(type: "TEXT", nullable: true),
                Genre = table.Column<string>(type: "TEXT", nullable: true),
                Metadata_ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                Metadata_Artwork_CoverImagePath = table.Column<string>(type: "TEXT", nullable: true),
                Metadata_Artwork_BackgroundImagePath = table.Column<string>(type: "TEXT", nullable: true),
                Metadata_Artwork_IconPath = table.Column<string>(type: "TEXT", nullable: true),
                IsInstalled = table.Column<bool>(type: "INTEGER", nullable: false),
                IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                SortingName = table.Column<string>(type: "TEXT", nullable: true),
                LastActivity = table.Column<DateTime>(type: "TEXT", nullable: true),
                Added = table.Column<DateTime>(type: "TEXT", nullable: true),
                PlaytimeSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                PlayCount = table.Column<long>(type: "INTEGER", nullable: false),
                Version = table.Column<string>(type: "TEXT", nullable: true),
                InstallDirectory = table.Column<string>(type: "TEXT", nullable: true),
                InstallSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                GameLibraryId = table.Column<Guid>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Games", item => item.Id);
                table.ForeignKey(
                    name: "FK_Games_Libraries_GameLibraryId",
                    column: item => item.GameLibraryId,
                    principalTable: "Libraries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GameLaunchActions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Target = table.Column<string>(type: "TEXT", nullable: false),
                Arguments = table.Column<string>(type: "TEXT", nullable: true),
                WorkingDirectory = table.Column<string>(type: "TEXT", nullable: true),
                IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                GameId = table.Column<Guid>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameLaunchActions", item => item.Id);
                table.ForeignKey(
                    name: "FK_GameLaunchActions_Games_GameId",
                    column: item => item.GameId,
                    principalTable: "Games",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Games_GameLibraryId",
            table: "Games",
            column: "GameLibraryId");

        migrationBuilder.CreateIndex(
            name: "IX_Games_SourceId_SourceGameId",
            table: "Games",
            columns: new[] { "SourceId", "SourceGameId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GameLaunchActions_GameId",
            table: "GameLaunchActions",
            column: "GameId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GameLaunchActions");
        migrationBuilder.DropTable(name: "Games");
        migrationBuilder.DropTable(name: "Libraries");
    }
}
