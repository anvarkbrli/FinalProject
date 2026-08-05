using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260805010000_GroupChatsAndBulkMessageSelection")]
public partial class GroupChatsAndBulkMessageSelection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ChatConversations_AspNetUsers_UserOneId",
            table: "ChatConversations");

        migrationBuilder.DropForeignKey(
            name: "FK_ChatConversations_AspNetUsers_UserTwoId",
            table: "ChatConversations");

        migrationBuilder.DropIndex(
            name: "IX_ChatConversations_UserOneId_RequestStatus_UpdatedAtUtc",
            table: "ChatConversations");

        migrationBuilder.DropIndex(
            name: "IX_ChatConversations_UserOneId_UserTwoId",
            table: "ChatConversations");

        migrationBuilder.DropIndex(
            name: "IX_ChatConversations_UserTwoId_RequestStatus_UpdatedAtUtc",
            table: "ChatConversations");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ChatConversations_DifferentUsers",
            table: "ChatConversations");

        migrationBuilder.AlterColumn<Guid>(
            name: "UserTwoId",
            table: "ChatConversations",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AlterColumn<Guid>(
            name: "UserOneId",
            table: "ChatConversations",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AddColumn<string>(
            name: "GroupDescription",
            table: "ChatConversations",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GroupImageUrl",
            table: "ChatConversations",
            type: "nvarchar(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GroupName",
            table: "ChatConversations",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsGroup",
            table: "ChatConversations",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "DeliveredAtUtc",
            table: "ChatMessageUserStates",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReadAtUtc",
            table: "ChatMessageUserStates",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAtUtc",
            table: "ChatConversationUserStates",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            table: "ChatConversationUserStates",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "ChatConversationMembers",
            columns: table => new
            {
                ConversationId = table.Column<long>(type: "bigint", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_ChatConversationMembers",
                    x => new { x.ConversationId, x.UserId });
                table.ForeignKey(
                    name: "FK_ChatConversationMembers_AspNetUsers_AddedByUserId",
                    column: x => x.AddedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ChatConversationMembers_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ChatConversationMembers_ChatConversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "ChatConversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_ChatConversations_ParticipantShape",
            table: "ChatConversations",
            sql: "([IsGroup] = 0 AND [UserOneId] IS NOT NULL AND [UserTwoId] IS NOT NULL AND [UserOneId] <> [UserTwoId] AND [GroupName] IS NULL) OR ([IsGroup] = 1 AND [UserOneId] IS NULL AND [UserTwoId] IS NULL AND [GroupName] IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversationMembers_AddedByUserId",
            table: "ChatConversationMembers",
            column: "AddedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversationMembers_UserId_JoinedAtUtc",
            table: "ChatConversationMembers",
            columns: new[] { "UserId", "JoinedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversationUserStates_UserId_IsDeleted_DeletedAtUtc",
            table: "ChatConversationUserStates",
            columns: new[] { "UserId", "IsDeleted", "DeletedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_IsGroup_UpdatedAtUtc",
            table: "ChatConversations",
            columns: new[] { "IsGroup", "UpdatedAtUtc" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_UserOneId_RequestStatus_UpdatedAtUtc",
            table: "ChatConversations",
            columns: new[] { "UserOneId", "RequestStatus", "UpdatedAtUtc" },
            descending: new[] { false, false, true });

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_UserOneId_UserTwoId",
            table: "ChatConversations",
            columns: new[] { "UserOneId", "UserTwoId" },
            unique: true,
            filter: "[IsGroup] = 0 AND [UserOneId] IS NOT NULL AND [UserTwoId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_UserTwoId_RequestStatus_UpdatedAtUtc",
            table: "ChatConversations",
            columns: new[] { "UserTwoId", "RequestStatus", "UpdatedAtUtc" },
            descending: new[] { false, false, true });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessageUserStates_UserId_DeliveredAtUtc",
            table: "ChatMessageUserStates",
            columns: new[] { "UserId", "DeliveredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessageUserStates_UserId_ReadAtUtc",
            table: "ChatMessageUserStates",
            columns: new[] { "UserId", "ReadAtUtc" });

        migrationBuilder.AddForeignKey(
            name: "FK_ChatConversations_AspNetUsers_UserOneId",
            table: "ChatConversations",
            column: "UserOneId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ChatConversations_AspNetUsers_UserTwoId",
            table: "ChatConversations",
            column: "UserTwoId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [ChatConversations] WHERE [IsGroup] = 1)
                THROW 51000, 'Rollback blocked: group conversations exist. Restore a pre-migration backup instead.', 1;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_ChatConversations_AspNetUsers_UserOneId",
            table: "ChatConversations");

        migrationBuilder.DropForeignKey(
            name: "FK_ChatConversations_AspNetUsers_UserTwoId",
            table: "ChatConversations");

        migrationBuilder.DropTable(name: "ChatConversationMembers");

        migrationBuilder.DropIndex(
            name: "IX_ChatConversationUserStates_UserId_IsDeleted_DeletedAtUtc",
            table: "ChatConversationUserStates");

        migrationBuilder.DropIndex(
            name: "IX_ChatConversations_IsGroup_UpdatedAtUtc",
            table: "ChatConversations");

        migrationBuilder.DropIndex(
            name: "IX_ChatConversations_UserOneId_RequestStatus_UpdatedAtUtc",
            table: "ChatConversations");

        migrationBuilder.DropIndex(
            name: "IX_ChatConversations_UserOneId_UserTwoId",
            table: "ChatConversations");

        migrationBuilder.DropIndex(
            name: "IX_ChatConversations_UserTwoId_RequestStatus_UpdatedAtUtc",
            table: "ChatConversations");

        migrationBuilder.DropIndex(
            name: "IX_ChatMessageUserStates_UserId_DeliveredAtUtc",
            table: "ChatMessageUserStates");

        migrationBuilder.DropIndex(
            name: "IX_ChatMessageUserStates_UserId_ReadAtUtc",
            table: "ChatMessageUserStates");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ChatConversations_ParticipantShape",
            table: "ChatConversations");

        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "ChatConversationUserStates");
        migrationBuilder.DropColumn(name: "IsDeleted", table: "ChatConversationUserStates");
        migrationBuilder.DropColumn(name: "GroupDescription", table: "ChatConversations");
        migrationBuilder.DropColumn(name: "GroupImageUrl", table: "ChatConversations");
        migrationBuilder.DropColumn(name: "GroupName", table: "ChatConversations");
        migrationBuilder.DropColumn(name: "IsGroup", table: "ChatConversations");
        migrationBuilder.DropColumn(name: "DeliveredAtUtc", table: "ChatMessageUserStates");
        migrationBuilder.DropColumn(name: "ReadAtUtc", table: "ChatMessageUserStates");

        migrationBuilder.AlterColumn<Guid>(
            name: "UserTwoId",
            table: "ChatConversations",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "UserOneId",
            table: "ChatConversations",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ChatConversations_DifferentUsers",
            table: "ChatConversations",
            sql: "[UserOneId] <> [UserTwoId]");

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_UserOneId_RequestStatus_UpdatedAtUtc",
            table: "ChatConversations",
            columns: new[] { "UserOneId", "RequestStatus", "UpdatedAtUtc" },
            descending: new[] { false, false, true });

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_UserOneId_UserTwoId",
            table: "ChatConversations",
            columns: new[] { "UserOneId", "UserTwoId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ChatConversations_UserTwoId_RequestStatus_UpdatedAtUtc",
            table: "ChatConversations",
            columns: new[] { "UserTwoId", "RequestStatus", "UpdatedAtUtc" },
            descending: new[] { false, false, true });

        migrationBuilder.AddForeignKey(
            name: "FK_ChatConversations_AspNetUsers_UserOneId",
            table: "ChatConversations",
            column: "UserOneId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ChatConversations_AspNetUsers_UserTwoId",
            table: "ChatConversations",
            column: "UserTwoId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
