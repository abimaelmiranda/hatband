using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseComplexGameCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompatibilityLayer_Tool_Discriminator",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompatibilityLayer_Discriminator",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Games SET CompatibilityLayer_Discriminator = 'CompatibilityLayer' " +
                "WHERE CompatibilityLayer_ExecutionMode IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE Games SET CompatibilityLayer_Tool_Discriminator = 'CompatibilityTool' " +
                "WHERE CompatibilityLayer_Discriminator IS NOT NULL " +
                "AND CompatibilityTool_InstallationPath IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "CompatibilityPrefix_IsManaged",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CompatibilityPrefix_Path",
                table: "Games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_Discriminator",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_Tool_Discriminator",
                table: "Games");

            migrationBuilder.AddColumn<string>(
                name: "CompatibilityPrefix_Path",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CompatibilityPrefix_IsManaged",
                table: "Games",
                type: "INTEGER",
                nullable: true);
        }
    }
}
