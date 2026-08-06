using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForumMediaNestedRepliesAndVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentReplyId",
                table: "DiscussionReplies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscussionMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiscussionId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionMedia_Discussions_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionReplyVotes",
                columns: table => new
                {
                    ReplyId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionReplyVotes", x => new { x.ReplyId, x.UserId });
                    table.CheckConstraint("CK_DiscussionReplyVotes_Value", "[Value] IN (-1, 1)");
                    table.ForeignKey(
                        name: "FK_DiscussionReplyVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscussionReplyVotes_DiscussionReplies_ReplyId",
                        column: x => x.ReplyId,
                        principalTable: "DiscussionReplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionReplies_ParentReplyId_CreatedAt",
                table: "DiscussionReplies",
                columns: new[] { "ParentReplyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionMedia_DiscussionId_SortOrder",
                table: "DiscussionMedia",
                columns: new[] { "DiscussionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionReplyVotes_UserId",
                table: "DiscussionReplyVotes",
                column: "UserId");

            // Preserve the existing reply likes as upvotes when the new Reddit-style
            // vote system is introduced.
            migrationBuilder.Sql(
                """
                INSERT INTO [DiscussionReplyVotes] ([ReplyId], [UserId], [Value], [CreatedAt])
                SELECT [ReplyId], [UserId], CAST(1 AS smallint), [CreatedAt]
                FROM [DiscussionReplyLikes]
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionReplies_DiscussionReplies_ParentReplyId",
                table: "DiscussionReplies",
                column: "ParentReplyId",
                principalTable: "DiscussionReplies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionReplies_DiscussionReplies_ParentReplyId",
                table: "DiscussionReplies");

            migrationBuilder.DropTable(
                name: "DiscussionMedia");

            migrationBuilder.DropTable(
                name: "DiscussionReplyVotes");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionReplies_ParentReplyId_CreatedAt",
                table: "DiscussionReplies");

            migrationBuilder.DropColumn(
                name: "ParentReplyId",
                table: "DiscussionReplies");
        }
    }
}
