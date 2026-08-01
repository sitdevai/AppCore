using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddVisualIdentityPatternColor : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PatternColor",
            schema: "app",
            table: "visual_identity_settings",
            type: "character varying(7)",
            maxLength: 7,
            nullable: false,
            defaultValue: "#1d4ed8");

        migrationBuilder.UpdateData(
            schema: "app",
            table: "visual_identity_settings",
            keyColumn: "Id",
            keyValue: 1,
            column: "PatternColor",
            value: "#1d4ed8");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PatternColor",
            schema: "app",
            table: "visual_identity_settings");
    }
}
