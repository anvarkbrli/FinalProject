using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260720090000_PostImagesAndRankedGameLists")]
    public partial class PostImagesAndRankedGameLists : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRanked",
                table: "GameLists",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "GameListItems",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "UserPosts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "UserPosts",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_GameListItems_Position",
                table: "GameListItems",
                sql: "[Position] IS NULL OR [Position] >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserPosts_ContentOrImage",
                table: "UserPosts",
                sql: "[Content] IS NOT NULL OR [ImageUrl] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GameListItems_GameListId_Position",
                table: "GameListItems",
                columns: new[] { "GameListId", "Position" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameListItems_GameListId_Position",
                table: "GameListItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GameListItems_Position",
                table: "GameListItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserPosts_ContentOrImage",
                table: "UserPosts");

            migrationBuilder.DropColumn(
                name: "IsRanked",
                table: "GameLists");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "GameListItems");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "UserPosts");

            migrationBuilder.Sql("UPDATE [UserPosts] SET [Content] = N'' WHERE [Content] IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "UserPosts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
