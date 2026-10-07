using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseComplexGameArtwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultArtwork_Discriminator",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Games\" SET \"DefaultArtwork_Discriminator\" = 'GameArtwork' " +
                "WHERE \"DefaultArtwork_CoverImagePath\" IS NOT NULL " +
                "OR \"DefaultArtwork_BackgroundImagePath\" IS NOT NULL " +
                "OR \"DefaultArtwork_IconPath\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultArtwork_Discriminator",
                table: "Games");
        }
    }
}
