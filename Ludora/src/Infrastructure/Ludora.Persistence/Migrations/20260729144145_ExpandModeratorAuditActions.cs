using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandModeratorAuditActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ModeratorActionLogs_ActionType",
                table: "ModeratorActionLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModeratorActionLogs_ActionType",
                table: "ModeratorActionLogs",
                sql: "[ActionType] BETWEEN 1 AND 6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ModeratorActionLogs_ActionType",
                table: "ModeratorActionLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModeratorActionLogs_ActionType",
                table: "ModeratorActionLogs",
                sql: "[ActionType] BETWEEN 1 AND 5");
        }
    }
}
