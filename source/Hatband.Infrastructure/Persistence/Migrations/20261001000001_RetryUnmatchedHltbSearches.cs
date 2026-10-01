using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hatband.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HatbandDbContext))]
[Migration(MigrationId)]
public sealed class RetryUnmatchedHltbSearches : Migration
{
    public const string MigrationId = "20261001000001_RetryUnmatchedHltbSearches";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE Games " +
            "SET TimeToBeat_LastSearchedAtUtc = NULL " +
            "WHERE TimeToBeat_GameId IS NULL " +
            "AND TimeToBeat_LastSearchedAtUtc IS NOT NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE Games " +
            "SET TimeToBeat_LastSearchedAtUtc = CURRENT_TIMESTAMP " +
            "WHERE TimeToBeat_GameId IS NULL " +
            "AND TimeToBeat_LastSearchedAtUtc IS NULL;");
    }
}
