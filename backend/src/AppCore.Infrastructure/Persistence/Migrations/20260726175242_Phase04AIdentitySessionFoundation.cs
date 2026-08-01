using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // EF Core generates composite-index column arrays.

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase04AIdentitySessionFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "security");

        migrationBuilder.EnsureSchema(
            name: "identity");

        migrationBuilder.CreateTable(
            name: "anonymous_pre_sessions",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_anonymous_pre_sessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "bootstrap_progress",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ProtectedOwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_bootstrap_progress", x => x.Id);
                table.CheckConstraint("CK_bootstrap_progress_singleton", "\"Id\" = 1");
            });

        migrationBuilder.CreateTable(
            name: "roles",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "security_audit_events",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                EventCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ResultCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                SourceIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                DetailsJson = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_security_audit_events", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AccountStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CredentialStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                MfaState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AuthorizationVersion = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TemporarilyThrottledUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsProtectedOwner = table.Column<bool>(type: "boolean", nullable: false),
                UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: true),
                SecurityStamp = table.Column<string>(type: "text", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                PhoneNumber = table.Column<string>(type: "text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "role_claims",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_role_claims", x => x.Id);
                table.ForeignKey(
                    name: "FK_role_claims_roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "mfa_authenticators",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ProtectedSecret = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                LastAcceptedTimeStep = table.Column<long>(type: "bigint", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mfa_authenticators", x => x.Id);
                table.ForeignKey(
                    name: "FK_mfa_authenticators_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "mfa_login_challenges",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                AnonymousPreSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mfa_login_challenges", x => x.Id);
                table.ForeignKey(
                    name: "FK_mfa_login_challenges_anonymous_pre_sessions_AnonymousPreSes~",
                    column: x => x.AnonymousPreSessionId,
                    principalSchema: "security",
                    principalTable: "anonymous_pre_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_mfa_login_challenges_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "mfa_recovery_codes",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                KeyedHash = table.Column<byte[]>(type: "bytea", maxLength: 64, nullable: false),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mfa_recovery_codes", x => x.Id);
                table.ForeignKey(
                    name: "FK_mfa_recovery_codes_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "restricted_recovery_sessions",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_restricted_recovery_sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_restricted_recovery_sessions_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "security_challenges",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Purpose = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                KeyedHash = table.Column<byte[]>(type: "bytea", maxLength: 64, nullable: false),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                InvalidatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_security_challenges", x => x.Id);
                table.ForeignKey(
                    name: "FK_security_challenges_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "sessions",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorizationVersion = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastActivityAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AbsoluteExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                MfaVerifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                AuthenticationMethods = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                DeviceLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ClientCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_sessions_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "user_claims",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_claims", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_claims_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_logins",
            schema: "identity",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                ProviderKey = table.Column<string>(type: "text", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_user_logins_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_roles",
            schema: "identity",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_user_roles_roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_roles_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_tokens",
            schema: "identity",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_user_tokens_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.InsertData(
            schema: "security",
            table: "bootstrap_progress",
            columns: new[] { "Id", "CompletedAtUtc", "ProtectedOwnerUserId", "State", "UpdatedAtUtc" },
            values: new object[] { 1, null, null, "NotStarted", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

        migrationBuilder.CreateIndex(
            name: "IX_anonymous_pre_sessions_ExpiresAtUtc",
            schema: "security",
            table: "anonymous_pre_sessions",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_mfa_authenticators_UserId",
            schema: "security",
            table: "mfa_authenticators",
            column: "UserId",
            unique: true,
            filter: "\"RevokedAtUtc\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_mfa_login_challenges_AnonymousPreSessionId",
            schema: "security",
            table: "mfa_login_challenges",
            column: "AnonymousPreSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_mfa_login_challenges_UserId",
            schema: "security",
            table: "mfa_login_challenges",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_mfa_recovery_codes_UserId_ConsumedAtUtc",
            schema: "security",
            table: "mfa_recovery_codes",
            columns: new[] { "UserId", "ConsumedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_restricted_recovery_sessions_UserId_RevokedAtUtc",
            schema: "security",
            table: "restricted_recovery_sessions",
            columns: new[] { "UserId", "RevokedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_role_claims_RoleId",
            schema: "identity",
            table: "role_claims",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            schema: "identity",
            table: "roles",
            column: "NormalizedName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_security_audit_events_ActorUserId",
            schema: "security",
            table: "security_audit_events",
            column: "ActorUserId");

        migrationBuilder.CreateIndex(
            name: "IX_security_audit_events_OccurredAtUtc",
            schema: "security",
            table: "security_audit_events",
            column: "OccurredAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_security_audit_events_TargetUserId",
            schema: "security",
            table: "security_audit_events",
            column: "TargetUserId");

        migrationBuilder.CreateIndex(
            name: "IX_security_challenges_UserId_Purpose",
            schema: "security",
            table: "security_challenges",
            columns: new[] { "UserId", "Purpose" },
            filter: "\"ConsumedAtUtc\" IS NULL AND \"InvalidatedAtUtc\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_sessions_UserId_RevokedAtUtc",
            schema: "security",
            table: "sessions",
            columns: new[] { "UserId", "RevokedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_user_claims_UserId",
            schema: "identity",
            table: "user_claims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_user_logins_UserId",
            schema: "identity",
            table: "user_logins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_user_roles_RoleId",
            schema: "identity",
            table: "user_roles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            schema: "identity",
            table: "users",
            column: "NormalizedEmail");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            schema: "identity",
            table: "users",
            column: "NormalizedUserName",
            unique: true);

        migrationBuilder.Sql(
            """
            CREATE FUNCTION security.reject_security_audit_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'security audit events are append-only';
            END;
            $$;

            CREATE TRIGGER security_audit_events_append_only
            BEFORE UPDATE OR DELETE ON security.security_audit_events
            FOR EACH ROW
            EXECUTE FUNCTION security.reject_security_audit_mutation();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS security_audit_events_append_only
                ON security.security_audit_events;
            DROP FUNCTION IF EXISTS security.reject_security_audit_mutation();
            """);

        migrationBuilder.DropTable(
            name: "bootstrap_progress",
            schema: "security");

        migrationBuilder.DropTable(
            name: "mfa_authenticators",
            schema: "security");

        migrationBuilder.DropTable(
            name: "mfa_login_challenges",
            schema: "security");

        migrationBuilder.DropTable(
            name: "mfa_recovery_codes",
            schema: "security");

        migrationBuilder.DropTable(
            name: "restricted_recovery_sessions",
            schema: "security");

        migrationBuilder.DropTable(
            name: "role_claims",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "security_audit_events",
            schema: "security");

        migrationBuilder.DropTable(
            name: "security_challenges",
            schema: "security");

        migrationBuilder.DropTable(
            name: "sessions",
            schema: "security");

        migrationBuilder.DropTable(
            name: "user_claims",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "user_logins",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "user_roles",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "user_tokens",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "anonymous_pre_sessions",
            schema: "security");

        migrationBuilder.DropTable(
            name: "roles",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "users",
            schema: "identity");
    }
}
