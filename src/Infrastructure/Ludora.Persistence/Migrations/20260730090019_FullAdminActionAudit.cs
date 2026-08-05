using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FullAdminActionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ModeratorActionLogs_ActionType",
                table: "ModeratorActionLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "TargetUserId",
                table: "ModeratorActionLogs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "ModeratorActionLogs",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceId",
                table: "ModeratorActionLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceName",
                table: "ModeratorActionLogs",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceType",
                table: "ModeratorActionLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModeratorActionLogs_ResourceType_CreatedAtUtc",
                table: "ModeratorActionLogs",
                columns: new[] { "ResourceType", "CreatedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModeratorActionLogs_ActionType",
                table: "ModeratorActionLogs",
                sql: "[ActionType] BETWEEN 1 AND 7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModeratorActionLogs_ResourceType_CreatedAtUtc",
                table: "ModeratorActionLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ModeratorActionLogs_ActionType",
                table: "ModeratorActionLogs");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "ModeratorActionLogs");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "ModeratorActionLogs");

            migrationBuilder.DropColumn(
                name: "ResourceName",
                table: "ModeratorActionLogs");

            migrationBuilder.DropColumn(
                name: "ResourceType",
                table: "ModeratorActionLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "TargetUserId",
                table: "ModeratorActionLogs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModeratorActionLogs_ActionType",
                table: "ModeratorActionLogs",
                sql: "[ActionType] BETWEEN 1 AND 6");
        }
    }
}
