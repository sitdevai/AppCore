using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Generated EF migration uses repeated metadata arrays.

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ExtendVisualIdentityAppearance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BackgroundColor",
            schema: "app",
            table: "visual_identity_settings",
            type: "character varying(7)",
            maxLength: 7,
            nullable: false,
            defaultValue: "#f4f6f8");

        migrationBuilder.AddColumn<string>(
            name: "BackgroundPattern",
            schema: "app",
            table: "visual_identity_settings",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "None");

        migrationBuilder.AddColumn<string>(
            name: "HeaderColor",
            schema: "app",
            table: "visual_identity_settings",
            type: "character varying(7)",
            maxLength: 7,
            nullable: false,
            defaultValue: "#ffffff");

        migrationBuilder.UpdateData(
            schema: "app",
            table: "visual_identity_settings",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "BackgroundColor", "BackgroundPattern", "HeaderColor" },
            values: new object[] { "#f4f6f8", "None", "#ffffff" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BackgroundColor",
            schema: "app",
            table: "visual_identity_settings");

        migrationBuilder.DropColumn(
            name: "BackgroundPattern",
            schema: "app",
            table: "visual_identity_settings");

        migrationBuilder.DropColumn(
            name: "HeaderColor",
            schema: "app",
            table: "visual_identity_settings");
    }
}
