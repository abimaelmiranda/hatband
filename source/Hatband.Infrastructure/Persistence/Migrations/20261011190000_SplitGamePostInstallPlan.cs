using Hatband.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HatbandDbContext))]
[Migration("20261011190000_SplitGamePostInstallPlan")]
public sealed partial class SplitGamePostInstallPlan : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GamePostInstallPlans",
            columns: table => new
            {
                GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                PostInstallPlan_Revision = table.Column<string>(type: "TEXT", nullable: false),
                PostInstallPlan_Steps = table.Column<string>(type: "TEXT", nullable: false),
                PostInstallPlan_Diagnostics = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GamePostInstallPlans", row => row.GameId);
                table.ForeignKey(
                    name: "FK_GamePostInstallPlans_Games_GameId",
                    column: row => row.GameId,
                    principalTable: "Games",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.DropColumn(name: "PostInstallPlan_Diagnostics", table: "Games");
        migrationBuilder.DropColumn(name: "PostInstallPlan_Discriminator", table: "Games");
        migrationBuilder.DropColumn(name: "PostInstallPlan_Revision", table: "Games");
        migrationBuilder.DropColumn(name: "PostInstallPlan_Steps", table: "Games");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "PostInstallPlan_Diagnostics", table: "Games", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PostInstallPlan_Discriminator", table: "Games", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PostInstallPlan_Revision", table: "Games", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PostInstallPlan_Steps", table: "Games", type: "TEXT", nullable: true);

        migrationBuilder.DropTable(name: "GamePostInstallPlans");
    }
}
