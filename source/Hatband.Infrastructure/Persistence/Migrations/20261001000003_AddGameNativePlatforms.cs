using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hatband.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HatbandDbContext))]
[Migration(MigrationId)]
public sealed class AddGameNativePlatforms : Migration
{
    public const string MigrationId = "20261001000003_AddGameNativePlatforms";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Metadata_NativePlatforms",
            table: "Games",
            type: "INTEGER",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Metadata_NativePlatforms",
            table: "Games");
    }
}
