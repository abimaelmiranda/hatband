using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameCompatibilityLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompatibilityLayer_Tier",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Games SET CompatibilityLayer_Tier = 5 WHERE CompatibilityTool_Name IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_Tier",
                table: "Games");
        }
    }
}
