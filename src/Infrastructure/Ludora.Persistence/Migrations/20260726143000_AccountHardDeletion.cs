using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    public partial class AccountHardDeletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccountDeletionRequestedAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeleteIfOwnershipTransferFails",
                table: "ForumCommunities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AccountDeletionRequestedAtUtc",
                table: "AspNetUsers",
                column: "AccountDeletionRequestedAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AccountDeletionRequestedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccountDeletionRequestedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DeleteIfOwnershipTransferFails",
                table: "ForumCommunities");
        }
    }
}
