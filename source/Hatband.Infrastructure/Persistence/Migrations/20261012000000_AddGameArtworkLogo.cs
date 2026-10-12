using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HatbandDbContext))]
[Migration("20261012000000_AddGameArtworkLogo")]
public sealed partial class AddGameArtworkLogo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Artwork_LogoPath",
            table: "Games",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DefaultArtwork_LogoPath",
            table: "Games",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Artwork_LogoPath", table: "Games");
        migrationBuilder.DropColumn(name: "DefaultArtwork_LogoPath", table: "Games");
    }
}
