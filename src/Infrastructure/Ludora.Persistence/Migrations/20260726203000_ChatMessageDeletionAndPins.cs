using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260726203000_ChatMessageDeletionAndPins")]
    public partial class ChatMessageDeletionAndPins : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatMessages_Content",
                table: "ChatMessages");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedForEveryoneAtUtc",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChatMessageUserStates",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PinnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_ChatMessageUserStates",
                        x => new { x.MessageId, x.UserId });
                    table.CheckConstraint(
                        "CK_ChatMessageUserStates_DeletedCannotBePinned",
                        "[IsDeleted] = 0 OR [PinnedAtUtc] IS NULL");
                    table.ForeignKey(
                        name: "FK_ChatMessageUserStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatMessageUserStates_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageUserStates_UserId_IsDeleted",
                table: "ChatMessageUserStates",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageUserStates_UserId_PinnedAtUtc",
                table: "ChatMessageUserStates",
                columns: new[] { "UserId", "PinnedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatMessages_Content",
                table: "ChatMessages",
                sql: "[DeletedForEveryoneAtUtc] IS NOT NULL OR [Text] IS NOT NULL OR [MediaStorageKey] IS NOT NULL OR [GameId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessageUserStates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatMessages_Content",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "DeletedForEveryoneAtUtc",
                table: "ChatMessages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatMessages_Content",
                table: "ChatMessages",
                sql: "[Text] IS NOT NULL OR [MediaStorageKey] IS NOT NULL OR [GameId] IS NOT NULL");
        }
    }
}
