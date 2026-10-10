using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomGameCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompatibilityLayer_AdditionalArguments",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompatibilityLayer_EnvironmentVariables",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompatibilityLayer_ExecutablePath",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompatibilityLayer_ExecutionMode",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompatibilityLayer_LaunchArguments",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompatibilityLayer_LaunchOptionId",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompatibilityLayer_WorkingDirectory",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Games SET CompatibilityLayer_ExecutionMode = 0 " +
                "WHERE CompatibilityLayer_Tier IS NOT NULL " +
                "OR CompatibilityTool_Name IS NOT NULL " +
                "OR CompatibilityPrefix_IsManaged IS NOT NULL " +
                "OR CompatibilityPrefix_Path IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE Games SET CompatibilityLayer_ExecutionMode = 1 " +
                "WHERE CompatibilityTool_InstallationPath IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE Games SET CompatibilityLayer_EnvironmentVariables = '{}' " +
                "WHERE CompatibilityLayer_ExecutionMode IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_AdditionalArguments",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_EnvironmentVariables",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_ExecutablePath",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_ExecutionMode",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_LaunchArguments",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_LaunchOptionId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CompatibilityLayer_WorkingDirectory",
                table: "Games");
        }
    }
}
