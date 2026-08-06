using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260722090000_ReportSecurityAndModerationAudit")]
    public partial class ReportSecurityAndModerationAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserReports_ReporterUserId_ReportedUserId",
                table: "UserReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserReports_Evidence",
                table: "UserReports");

            migrationBuilder.AddColumn<long>(
                name: "ReportedContentId",
                table: "UserReports",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedContentSnapshot",
                table: "UserReports",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportedContentType",
                table: "UserReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReportedContentUrl",
                table: "UserReports",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserReports_ContentReference",
                table: "UserReports",
                sql: "([ReportedContentType] = 0 AND [ReportedContentId] IS NULL) OR ([ReportedContentType] <> 0 AND [ReportedContentId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserReports_ContentType",
                table: "UserReports",
                sql: "[ReportedContentType] BETWEEN 0 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserReports_Evidence",
                table: "UserReports",
                sql: "[Description] IS NOT NULL OR [EvidenceImageStorageKey] IS NOT NULL OR [ReportedContentId] IS NOT NULL");

            migrationBuilder.CreateTable(
                name: "ModeratorActionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserReportId = table.Column<long>(type: "bigint", nullable: true),
                    UserWarningId = table.Column<int>(type: "int", nullable: true),
                    UserBanHistoryId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    PreviousReportStatus = table.Column<int>(type: "int", nullable: true),
                    NewReportStatus = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModeratorActionLogs", x => x.Id);
                    table.CheckConstraint(
                        "CK_ModeratorActionLogs_ActionType",
                        "[ActionType] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_ModeratorActionLogs_AspNetUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModeratorActionLogs_AspNetUsers_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModeratorActionLogs_UserBanHistories_UserBanHistoryId",
                        column: x => x.UserBanHistoryId,
                        principalTable: "UserBanHistories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModeratorActionLogs_UserReports_UserReportId",
                        column: x => x.UserReportId,
                        principalTable: "UserReports",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModeratorActionLogs_UserWarnings_UserWarningId",
                        column: x => x.UserWarningId,
                        principalTable: "UserWarnings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReportedContentType_ReportedContentId",
                table: "UserReports",
                columns: new[] { "ReportedContentType", "ReportedContentId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReporterUserId_ReportedUserId_ReportedContentType_ReportedContentId",
                table: "UserReports",
                columns: new[]
                {
                    "ReporterUserId",
                    "ReportedUserId",
                    "ReportedContentType",
                    "ReportedContentId"
                },
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_ModeratorActionLogs_AdminUserId_CreatedAtUtc",
                table: "ModeratorActionLogs",
                columns: new[] { "AdminUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ModeratorActionLogs_TargetUserId_CreatedAtUtc",
                table: "ModeratorActionLogs",
                columns: new[] { "TargetUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ModeratorActionLogs_UserBanHistoryId",
                table: "ModeratorActionLogs",
                column: "UserBanHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeratorActionLogs_UserReportId_CreatedAtUtc",
                table: "ModeratorActionLogs",
                columns: new[] { "UserReportId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ModeratorActionLogs_UserWarningId",
                table: "ModeratorActionLogs",
                column: "UserWarningId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModeratorActionLogs");

            migrationBuilder.DropIndex(
                name: "IX_UserReports_ReportedContentType_ReportedContentId",
                table: "UserReports");

            migrationBuilder.DropIndex(
                name: "IX_UserReports_ReporterUserId_ReportedUserId_ReportedContentType_ReportedContentId",
                table: "UserReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserReports_ContentReference",
                table: "UserReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserReports_ContentType",
                table: "UserReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserReports_Evidence",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReportedContentId",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReportedContentSnapshot",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReportedContentType",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReportedContentUrl",
                table: "UserReports");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserReports_Evidence",
                table: "UserReports",
                sql: "[Description] IS NOT NULL OR [EvidenceImageStorageKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReporterUserId_ReportedUserId",
                table: "UserReports",
                columns: new[] { "ReporterUserId", "ReportedUserId" },
                unique: true,
                filter: "[Status] IN (0, 1)");
        }
    }
}
