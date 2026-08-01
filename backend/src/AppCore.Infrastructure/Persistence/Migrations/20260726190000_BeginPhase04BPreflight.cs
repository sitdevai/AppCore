using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppCore.Infrastructure.Persistence.Migrations;

public partial class BeginPhase04BPreflight : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_mfa_login_challenges_UserId",
            schema: "security",
            table: "mfa_login_challenges");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "InvalidatedAtUtc",
            schema: "security",
            table: "mfa_login_challenges",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_mfa_login_challenges_UserId",
            schema: "security",
            table: "mfa_login_challenges",
            column: "UserId",
            unique: true,
            filter: "\"ConsumedAtUtc\" IS NULL AND \"InvalidatedAtUtc\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_mfa_login_challenges_UserId",
            schema: "security",
            table: "mfa_login_challenges");

        migrationBuilder.DropColumn(
            name: "InvalidatedAtUtc",
            schema: "security",
            table: "mfa_login_challenges");

        migrationBuilder.CreateIndex(
            name: "IX_mfa_login_challenges_UserId",
            schema: "security",
            table: "mfa_login_challenges",
            column: "UserId");
    }
}
