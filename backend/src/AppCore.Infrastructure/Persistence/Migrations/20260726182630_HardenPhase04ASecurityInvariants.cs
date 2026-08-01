using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // EF Core generates composite-index column arrays.

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class HardenPhase04ASecurityInvariants : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "EmailIndex",
            schema: "identity",
            table: "users");

        migrationBuilder.DropIndex(
            name: "IX_security_challenges_UserId_Purpose",
            schema: "security",
            table: "security_challenges");

        migrationBuilder.CreateTable(
            name: "security_audit_contexts",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SecurityAuditEventId = table.Column<long>(type: "bigint", nullable: false),
                SourceIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_security_audit_contexts", x => x.Id);
                table.ForeignKey(
                    name: "FK_security_audit_contexts_security_audit_events_SecurityAudit~",
                    column: x => x.SecurityAuditEventId,
                    principalSchema: "security",
                    principalTable: "security_audit_events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO security.security_audit_contexts
                ("SecurityAuditEventId", "SourceIp", "UserAgent", "ExpiresAtUtc")
            SELECT "Id", "SourceIp", "UserAgent", "OccurredAtUtc" + INTERVAL '90 days'
            FROM security.security_audit_events
            WHERE "SourceIp" IS NOT NULL OR "UserAgent" IS NOT NULL;
            """);

        migrationBuilder.DropColumn(
            name: "SourceIp",
            schema: "security",
            table: "security_audit_events");

        migrationBuilder.DropColumn(
            name: "UserAgent",
            schema: "security",
            table: "security_audit_events");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            schema: "identity",
            table: "users",
            column: "NormalizedEmail",
            unique: true,
            filter: "\"NormalizedEmail\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_security_challenges_UserId_Purpose",
            schema: "security",
            table: "security_challenges",
            columns: new[] { "UserId", "Purpose" },
            unique: true,
            filter: "\"ConsumedAtUtc\" IS NULL AND \"InvalidatedAtUtc\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_security_audit_contexts_ExpiresAtUtc",
            schema: "security",
            table: "security_audit_contexts",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_security_audit_contexts_SecurityAuditEventId",
            schema: "security",
            table: "security_audit_contexts",
            column: "SecurityAuditEventId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "security_audit_contexts",
            schema: "security");

        migrationBuilder.DropIndex(
            name: "EmailIndex",
            schema: "identity",
            table: "users");

        migrationBuilder.DropIndex(
            name: "IX_security_challenges_UserId_Purpose",
            schema: "security",
            table: "security_challenges");

        migrationBuilder.AddColumn<string>(
            name: "SourceIp",
            schema: "security",
            table: "security_audit_events",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UserAgent",
            schema: "security",
            table: "security_audit_events",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            schema: "identity",
            table: "users",
            column: "NormalizedEmail");

        migrationBuilder.CreateIndex(
            name: "IX_security_challenges_UserId_Purpose",
            schema: "security",
            table: "security_challenges",
            columns: new[] { "UserId", "Purpose" },
            filter: "\"ConsumedAtUtc\" IS NULL AND \"InvalidatedAtUtc\" IS NULL");
    }
}
