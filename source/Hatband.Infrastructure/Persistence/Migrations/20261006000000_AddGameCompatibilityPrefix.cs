using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hatband.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddGameCompatibilityPrefix : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "CompatibilityPrefix_IsManaged",
            table: "Games",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CompatibilityPrefix_Path",
            table: "Games",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CompatibilityPrefix_IsManaged",
            table: "Games");

        migrationBuilder.DropColumn(
            name: "CompatibilityPrefix_Path",
            table: "Games");
    }
}
