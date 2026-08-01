using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Generated EF migration uses repeated metadata arrays.

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase05VisualIdentity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "app");

        migrationBuilder.CreateTable(
            name: "branding_assets",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                StoredFileName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Length = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_branding_assets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "visual_identity_settings",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ShortOrganizationName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                PrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                SecondaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                LightLogoAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                DarkLogoAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                CompactLogoAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                FaviconAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                Version = table.Column<long>(type: "bigint", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_visual_identity_settings", x => x.Id);
                table.CheckConstraint("CK_visual_identity_singleton", "\"Id\" = 1");
            });

        migrationBuilder.InsertData(
            schema: "security",
            table: "permissions",
            columns: new[] { "Id", "Assurance", "Scope" },
            values: new object[,]
            {
                { "Settings.VisualIdentity.Update", "HighRisk", "GlobalSystem" },
                { "Settings.VisualIdentity.View", "Sensitive", "GlobalSystem" }
            });

        migrationBuilder.InsertData(
            schema: "app",
            table: "visual_identity_settings",
            columns: new[] { "Id", "CompactLogoAssetId", "DarkLogoAssetId", "FaviconAssetId", "LightLogoAssetId", "OrganizationName", "PrimaryColor", "SecondaryColor", "ShortOrganizationName", "UpdatedAtUtc", "Version" },
            values: new object[] { 1, null, null, null, null, "AppCore", "#1d4ed8", "#0f766e", "Core", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1L });

        migrationBuilder.InsertData(
            schema: "security",
            table: "role_permissions",
            columns: new[] { "PermissionId", "RoleId" },
            values: new object[,]
            {
                { "Settings.VisualIdentity.Update", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Settings.VisualIdentity.View", new Guid("10000000-0000-0000-0000-000000000001") }
            });

        migrationBuilder.CreateIndex(
            name: "IX_branding_assets_StoredFileName",
            schema: "app",
            table: "branding_assets",
            column: "StoredFileName",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "branding_assets",
            schema: "app");

        migrationBuilder.DropTable(
            name: "visual_identity_settings",
            schema: "app");

        migrationBuilder.DeleteData(
            schema: "security",
            table: "role_permissions",
            keyColumns: new[] { "PermissionId", "RoleId" },
            keyValues: new object[] { "Settings.VisualIdentity.Update", new Guid("10000000-0000-0000-0000-000000000001") });

        migrationBuilder.DeleteData(
            schema: "security",
            table: "role_permissions",
            keyColumns: new[] { "PermissionId", "RoleId" },
            keyValues: new object[] { "Settings.VisualIdentity.View", new Guid("10000000-0000-0000-0000-000000000001") });

        migrationBuilder.DeleteData(
            schema: "security",
            table: "permissions",
            keyColumn: "Id",
            keyValue: "Settings.VisualIdentity.Update");

        migrationBuilder.DeleteData(
            schema: "security",
            table: "permissions",
            keyColumn: "Id",
            keyValue: "Settings.VisualIdentity.View");
    }
}
