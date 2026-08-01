using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Generated EF migration uses repeated metadata arrays.

namespace AppCore.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase04CAuthorizationFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsArchived",
            schema: "identity",
            table: "roles",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsBuiltIn",
            schema: "identity",
            table: "roles",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsProtected",
            schema: "identity",
            table: "roles",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "permissions",
            schema: "security",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Assurance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_permissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "role_permissions",
            schema: "security",
            columns: table => new
            {
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                PermissionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_role_permissions", x => new { x.RoleId, x.PermissionId });
                table.ForeignKey(
                    name: "FK_role_permissions_permissions_PermissionId",
                    column: x => x.PermissionId,
                    principalSchema: "security",
                    principalTable: "permissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_role_permissions_roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            schema: "security",
            table: "permissions",
            columns: new[] { "Id", "Assurance", "Scope" },
            values: new object[,]
            {
                { "Audit.Security.Export", "HighRisk", "GlobalSystem" },
                { "Audit.Security.View", "Sensitive", "GlobalSystem" },
                { "Permissions.AssignToRoles", "HighRisk", "GlobalSystem" },
                { "Permissions.View", "Sensitive", "GlobalSystem" },
                { "Roles.Archive", "HighRisk", "GlobalSystem" },
                { "Roles.AssignToUsers", "HighRisk", "AllUsers" },
                { "Roles.Create", "HighRisk", "GlobalSystem" },
                { "Roles.Update", "HighRisk", "GlobalSystem" },
                { "Roles.View", "Sensitive", "GlobalSystem" },
                { "Sessions.RevokeForUser", "HighRisk", "AllUsers" },
                { "Sessions.RevokeGlobal", "Emergency", "GlobalSystem" },
                { "Sessions.RevokeOwn", "Standard", "OwnAccount" },
                { "Sessions.ViewForUser", "Sensitive", "AllUsers" },
                { "Sessions.ViewOwn", "Standard", "OwnAccount" },
                { "Users.Archive", "HighRisk", "AllUsers" },
                { "Users.Create", "HighRisk", "AllUsers" },
                { "Users.Disable", "HighRisk", "AllUsers" },
                { "Users.Enable", "HighRisk", "AllUsers" },
                { "Users.IssueActivation", "HighRisk", "AllUsers" },
                { "Users.IssueMfaRecovery", "HighRisk", "AllUsers" },
                { "Users.ResetMfa", "HighRisk", "AllUsers" },
                { "Users.ResetPassword", "HighRisk", "AllUsers" },
                { "Users.Restore", "HighRisk", "AllUsers" },
                { "Users.RevokeAuthenticators", "HighRisk", "AllUsers" },
                { "Users.Suspend", "HighRisk", "AllUsers" },
                { "Users.Update", "HighRisk", "AllUsers" },
                { "Users.View", "Sensitive", "AllUsers" }
            });

        migrationBuilder.InsertData(
            schema: "identity",
            table: "roles",
            columns: new[] { "Id", "ConcurrencyStamp", "IsBuiltIn", "IsProtected", "Name", "NormalizedName" },
            values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), "phase-04c-10000000000000000000000000000001", true, true, "System Administrator", "SYSTEM ADMINISTRATOR" });

        migrationBuilder.InsertData(
            schema: "identity",
            table: "roles",
            columns: new[] { "Id", "ConcurrencyStamp", "IsBuiltIn", "Name", "NormalizedName" },
            values: new object[,]
            {
                { new Guid("10000000-0000-0000-0000-000000000002"), "phase-04c-10000000000000000000000000000002", true, "User Administrator", "USER ADMINISTRATOR" },
                { new Guid("10000000-0000-0000-0000-000000000003"), "phase-04c-10000000000000000000000000000003", true, "Security Administrator", "SECURITY ADMINISTRATOR" },
                { new Guid("10000000-0000-0000-0000-000000000004"), "phase-04c-10000000000000000000000000000004", true, "Application User", "DIRECTORATE USER" },
                { new Guid("10000000-0000-0000-0000-000000000005"), "phase-04c-10000000000000000000000000000005", true, "Manager / Approver", "MANAGER / APPROVER" },
                { new Guid("10000000-0000-0000-0000-000000000006"), "phase-04c-10000000000000000000000000000006", true, "Auditor / Reporting User", "AUDITOR / REPORTING USER" }
            });

        migrationBuilder.InsertData(
            schema: "security",
            table: "role_permissions",
            columns: new[] { "PermissionId", "RoleId" },
            values: new object[,]
            {
                { "Audit.Security.Export", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Audit.Security.View", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Permissions.AssignToRoles", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Permissions.View", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Roles.Archive", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Roles.AssignToUsers", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Roles.Create", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Roles.Update", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Roles.View", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Sessions.RevokeForUser", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Sessions.RevokeGlobal", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Sessions.RevokeOwn", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Sessions.ViewForUser", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Sessions.ViewOwn", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.Archive", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.Create", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.Disable", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.Enable", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.IssueActivation", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.IssueMfaRecovery", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.ResetMfa", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.ResetPassword", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.Restore", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.RevokeAuthenticators", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.Suspend", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.Update", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Users.View", new Guid("10000000-0000-0000-0000-000000000001") },
                { "Permissions.View", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Roles.AssignToUsers", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Roles.View", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Sessions.RevokeOwn", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Sessions.ViewOwn", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.Archive", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.Create", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.Disable", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.Enable", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.IssueActivation", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.ResetPassword", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.Restore", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.Suspend", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.Update", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Users.View", new Guid("10000000-0000-0000-0000-000000000002") },
                { "Audit.Security.Export", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Audit.Security.View", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Permissions.AssignToRoles", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Permissions.View", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Roles.Archive", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Roles.Create", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Roles.Update", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Roles.View", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Sessions.RevokeForUser", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Sessions.RevokeGlobal", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Sessions.RevokeOwn", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Sessions.ViewForUser", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Sessions.ViewOwn", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Users.IssueMfaRecovery", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Users.ResetMfa", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Users.RevokeAuthenticators", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Users.View", new Guid("10000000-0000-0000-0000-000000000003") },
                { "Sessions.RevokeOwn", new Guid("10000000-0000-0000-0000-000000000004") },
                { "Sessions.ViewOwn", new Guid("10000000-0000-0000-0000-000000000004") },
                { "Sessions.RevokeOwn", new Guid("10000000-0000-0000-0000-000000000005") },
                { "Sessions.ViewOwn", new Guid("10000000-0000-0000-0000-000000000005") },
                { "Audit.Security.View", new Guid("10000000-0000-0000-0000-000000000006") },
                { "Permissions.View", new Guid("10000000-0000-0000-0000-000000000006") },
                { "Roles.View", new Guid("10000000-0000-0000-0000-000000000006") },
                { "Sessions.RevokeOwn", new Guid("10000000-0000-0000-0000-000000000006") },
                { "Sessions.ViewOwn", new Guid("10000000-0000-0000-0000-000000000006") }
            });

        migrationBuilder.CreateIndex(
            name: "IX_role_permissions_PermissionId",
            schema: "security",
            table: "role_permissions",
            column: "PermissionId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "role_permissions",
            schema: "security");

        migrationBuilder.DropTable(
            name: "permissions",
            schema: "security");

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000003"));

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000004"));

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000005"));

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000006"));

        migrationBuilder.DropColumn(
            name: "IsArchived",
            schema: "identity",
            table: "roles");

        migrationBuilder.DropColumn(
            name: "IsBuiltIn",
            schema: "identity",
            table: "roles");

        migrationBuilder.DropColumn(
            name: "IsProtected",
            schema: "identity",
            table: "roles");
    }
}
