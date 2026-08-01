using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SynchronizeAppCoreTemplateSeed : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            schema: "identity",
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
            column: "NormalizedName",
            value: "APPLICATION USER");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            schema: "identity",
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
            column: "NormalizedName",
            value: "DIRECTORATE USER");
    }
}
