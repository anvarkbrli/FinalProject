using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserBanHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserBanHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BannedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BanType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BannedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnbannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnbannedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBanHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBanHistories_AspNetUsers_BannedByUserId",
                        column: x => x.BannedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBanHistories_AspNetUsers_UnbannedByUserId",
                        column: x => x.UnbannedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBanHistories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserBanHistories_BannedByUserId",
                table: "UserBanHistories",
                column: "BannedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBanHistories_UnbannedByUserId",
                table: "UserBanHistories",
                column: "UnbannedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBanHistories_UserId_BannedAtUtc",
                table: "UserBanHistories",
                columns: new[] { "UserId", "BannedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBanHistories_UserId_UnbannedAtUtc",
                table: "UserBanHistories",
                columns: new[] { "UserId", "UnbannedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBanHistories");
        }
    }
}
