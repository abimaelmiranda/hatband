using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGamePostInstallPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostInstallPlan_Diagnostics",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostInstallPlan_Discriminator",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostInstallPlan_Revision",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostInstallPlan_Steps",
                table: "Games",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PostInstallPlan_Diagnostics",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "PostInstallPlan_Discriminator",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "PostInstallPlan_Revision",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "PostInstallPlan_Steps",
                table: "Games");
        }
    }
}
