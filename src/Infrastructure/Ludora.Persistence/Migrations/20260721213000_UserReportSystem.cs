using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    public partial class UserReportSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceImageStorageKey = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReports", x => x.Id);
                    table.CheckConstraint(
                        "CK_UserReports_Category",
                        "[Category] BETWEEN 1 AND 9");
                    table.CheckConstraint(
                        "CK_UserReports_DifferentUsers",
                        "[ReporterUserId] <> [ReportedUserId]");
                    table.CheckConstraint(
                        "CK_UserReports_Evidence",
                        "[Description] IS NOT NULL OR [EvidenceImageStorageKey] IS NOT NULL");
                    table.CheckConstraint(
                        "CK_UserReports_Status",
                        "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_UserReports_AspNetUsers_ReportedUserId",
                        column: x => x.ReportedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserReports_AspNetUsers_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserReports_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReportedUserId_Category",
                table: "UserReports",
                columns: new[] { "ReportedUserId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReportedUserId_Status_CreatedAtUtc",
                table: "UserReports",
                columns: new[] { "ReportedUserId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReporterUserId_CreatedAtUtc",
                table: "UserReports",
                columns: new[] { "ReporterUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReporterUserId_ReportedUserId",
                table: "UserReports",
                columns: new[] { "ReporterUserId", "ReportedUserId" },
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReviewedByUserId",
                table: "UserReports",
                column: "ReviewedByUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserReports");
        }
    }
}
