using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityModeratorsAndOwnershipTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnershipTransferDeclinedUserIdsJson",
                table: "ForumCommunities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnershipTransferStartedAt",
                table: "ForumCommunities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingOwnerCandidateId",
                table: "ForumCommunities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumCommunities_PendingOwnerCandidateId",
                table: "ForumCommunities",
                column: "PendingOwnerCandidateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ForumCommunities_PendingOwnerCandidateId",
                table: "ForumCommunities");

            migrationBuilder.DropColumn(
                name: "OwnershipTransferDeclinedUserIdsJson",
                table: "ForumCommunities");

            migrationBuilder.DropColumn(
                name: "OwnershipTransferStartedAt",
                table: "ForumCommunities");

            migrationBuilder.DropColumn(
                name: "PendingOwnerCandidateId",
                table: "ForumCommunities");
        }
    }
}
