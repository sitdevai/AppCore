using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // EF Core generates composite-index column arrays.

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ImplementPhase04BAuthentication : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "FailedLoginWindowStartedAtUtc",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "VerifiedAtUtc",
            schema: "security",
            table: "mfa_authenticators",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "password_history",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_password_history", x => x.Id);
                table.ForeignKey(
                    name: "FK_password_history_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_password_history_UserId_CreatedAtUtc",
            schema: "security",
            table: "password_history",
            columns: new[] { "UserId", "CreatedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "password_history",
            schema: "security");

        migrationBuilder.DropColumn(
            name: "FailedLoginWindowStartedAtUtc",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "VerifiedAtUtc",
            schema: "security",
            table: "mfa_authenticators");
    }
}
