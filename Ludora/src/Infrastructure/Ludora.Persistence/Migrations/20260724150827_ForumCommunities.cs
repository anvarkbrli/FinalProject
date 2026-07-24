using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForumCommunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserReports_ContentType",
                table: "UserReports");

            migrationBuilder.AddColumn<int>(
                name: "CommunityId",
                table: "Discussions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ForumCommunities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IconUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumCommunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForumCommunities_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForumCommunityFollows",
                columns: table => new
                {
                    CommunityId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumCommunityFollows", x => new { x.CommunityId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ForumCommunityFollows_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ForumCommunityFollows_ForumCommunities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "ForumCommunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForumCommunityMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommunityId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumCommunityMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForumCommunityMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ForumCommunityMembers_ForumCommunities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "ForumCommunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserReports_ContentType",
                table: "UserReports",
                sql: "[ReportedContentType] BETWEEN 0 AND 8");

            migrationBuilder.CreateIndex(
                name: "IX_Discussions_CommunityId_CreatedAt",
                table: "Discussions",
                columns: new[] { "CommunityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ForumCommunities_Category_CreatedAt",
                table: "ForumCommunities",
                columns: new[] { "Category", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ForumCommunities_Name",
                table: "ForumCommunities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumCommunities_OwnerId",
                table: "ForumCommunities",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumCommunities_Slug",
                table: "ForumCommunities",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumCommunities_Visibility_CreatedAt",
                table: "ForumCommunities",
                columns: new[] { "Visibility", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ForumCommunityFollows_UserId_CreatedAt",
                table: "ForumCommunityFollows",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ForumCommunityMembers_CommunityId_UserId",
                table: "ForumCommunityMembers",
                columns: new[] { "CommunityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumCommunityMembers_UserId_Status_CreatedAt",
                table: "ForumCommunityMembers",
                columns: new[] { "UserId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Discussions_ForumCommunities_CommunityId",
                table: "Discussions",
                column: "CommunityId",
                principalTable: "ForumCommunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Discussions_ForumCommunities_CommunityId",
                table: "Discussions");

            migrationBuilder.DropTable(
                name: "ForumCommunityFollows");

            migrationBuilder.DropTable(
                name: "ForumCommunityMembers");

            migrationBuilder.DropTable(
                name: "ForumCommunities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserReports_ContentType",
                table: "UserReports");

            migrationBuilder.DropIndex(
                name: "IX_Discussions_CommunityId_CreatedAt",
                table: "Discussions");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                table: "Discussions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserReports_ContentType",
                table: "UserReports",
                sql: "[ReportedContentType] BETWEEN 0 AND 7");
        }
    }
}
