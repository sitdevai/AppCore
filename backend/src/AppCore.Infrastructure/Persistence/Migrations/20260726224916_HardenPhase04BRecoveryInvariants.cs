using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class HardenPhase04BRecoveryInvariants : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_restricted_recovery_sessions_UserId_RevokedAtUtc",
            schema: "security",
            table: "restricted_recovery_sessions");

        migrationBuilder.CreateIndex(
            name: "IX_restricted_recovery_sessions_UserId",
            schema: "security",
            table: "restricted_recovery_sessions",
            column: "UserId",
            unique: true,
            filter: "\"RevokedAtUtc\" IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_restricted_recovery_sessions_UserId",
            schema: "security",
            table: "restricted_recovery_sessions");

#pragma warning disable CA1861
        migrationBuilder.CreateIndex(
            name: "IX_restricted_recovery_sessions_UserId_RevokedAtUtc",
        schema: "security",
        table: "restricted_recovery_sessions",
            columns: new[] { "UserId", "RevokedAtUtc" });
#pragma warning restore CA1861
    }
}
