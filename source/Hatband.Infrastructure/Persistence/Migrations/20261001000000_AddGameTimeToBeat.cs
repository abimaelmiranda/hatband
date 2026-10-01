using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hatband.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HatbandDbContext))]
[Migration(MigrationId)]
public sealed class AddGameTimeToBeat : Migration
{
    public const string MigrationId = "20261001000000_AddGameTimeToBeat";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "TimeToBeat_GameId",
            table: "Games",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TimeToBeat_GameName",
            table: "Games",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "TimeToBeat_MainStorySeconds",
            table: "Games",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "TimeToBeat_MainStoryPlusExtrasSeconds",
            table: "Games",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "TimeToBeat_CompletionistSeconds",
            table: "Games",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "TimeToBeat_LastSearchedAtUtc",
            table: "Games",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "TimeToBeat_GameId", table: "Games");
        migrationBuilder.DropColumn(name: "TimeToBeat_GameName", table: "Games");
        migrationBuilder.DropColumn(name: "TimeToBeat_MainStorySeconds", table: "Games");
        migrationBuilder.DropColumn(name: "TimeToBeat_MainStoryPlusExtrasSeconds", table: "Games");
        migrationBuilder.DropColumn(name: "TimeToBeat_CompletionistSeconds", table: "Games");
        migrationBuilder.DropColumn(name: "TimeToBeat_LastSearchedAtUtc", table: "Games");
    }
}
