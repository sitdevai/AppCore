using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ClosePhase04BSecurityGaps : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DELETE FROM security.mfa_login_challenges;");

        migrationBuilder.AddColumn<Guid>(
            name: "AuthenticatorId",
            schema: "security",
            table: "mfa_login_challenges",
            type: "uuid",
            nullable: false);

        migrationBuilder.AddColumn<long>(
            name: "AuthorizationVersionAtIssue",
            schema: "security",
            table: "mfa_login_challenges",
            type: "bigint",
            nullable: false);

        migrationBuilder.CreateIndex(
            name: "IX_mfa_login_challenges_AuthenticatorId",
            schema: "security",
            table: "mfa_login_challenges",
            column: "AuthenticatorId");

        migrationBuilder.AddForeignKey(
            name: "FK_mfa_login_challenges_mfa_authenticators_AuthenticatorId",
            schema: "security",
            table: "mfa_login_challenges",
            column: "AuthenticatorId",
            principalSchema: "security",
            principalTable: "mfa_authenticators",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_mfa_login_challenges_mfa_authenticators_AuthenticatorId",
            schema: "security",
            table: "mfa_login_challenges");

        migrationBuilder.DropIndex(
            name: "IX_mfa_login_challenges_AuthenticatorId",
            schema: "security",
            table: "mfa_login_challenges");

        migrationBuilder.DropColumn(
            name: "AuthenticatorId",
            schema: "security",
            table: "mfa_login_challenges");

        migrationBuilder.DropColumn(
            name: "AuthorizationVersionAtIssue",
            schema: "security",
            table: "mfa_login_challenges");
    }
}
