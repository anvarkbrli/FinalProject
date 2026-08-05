using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexesAndOutboxDeadLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPosts_IsDeleted_CreatedAt",
                table: "UserPosts");

            migrationBuilder.DropIndex(
                name: "IX_UserPosts_UserId_CreatedAt",
                table: "UserPosts");

            migrationBuilder.DropIndex(
                name: "IX_UserFollows_FollowingId",
                table: "UserFollows");

            migrationBuilder.DropIndex(
                name: "IX_UserBlocks_BlockedUserId",
                table: "UserBlocks");

            migrationBuilder.DropIndex(
                name: "IX_UserActivities_UserId_CreatedAt",
                table: "UserActivities");

            migrationBuilder.DropIndex(
                name: "IX_SteamOwnedGames_UserId_PlaytimeMinutes",
                table: "SteamOwnedGames");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientUserId_IsRead_Priority_CreatedAtUtc",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientUserId_Type_CreatedAtUtc",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_NotificationOutboxMessages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntilUtc_CreatedAtUtc",
                table: "NotificationOutboxMessages");

            migrationBuilder.Sql(
                """
                UPDATE [ReviewComments]
                SET [Content] = N''
                WHERE [Content] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "ReviewComments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAtUtc",
                table: "NotificationOutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPosts_IsDeleted_CreatedAt_Id",
                table: "UserPosts",
                columns: new[] { "IsDeleted", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserPosts_UserId_CreatedAt_Id",
                table: "UserPosts",
                columns: new[] { "UserId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserGames_Source_GameId",
                table: "UserGames",
                columns: new[] { "Source", "GameId" })
                .Annotation("SqlServer:Include", new[] { "Rating", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGames_UserId_UpdatedAt_CreatedAt_Id",
                table: "UserGames",
                columns: new[] { "UserId", "UpdatedAt", "CreatedAt", "Id" },
                descending: new[] { false, true, true, true })
                .Annotation("SqlServer:Include", new[] { "Status", "IsFavorite", "IsWishlisted", "Rating" });

            migrationBuilder.CreateIndex(
                name: "IX_UserFollows_FollowerId_Status_CreatedAt_FollowingId",
                table: "UserFollows",
                columns: new[] { "FollowerId", "Status", "CreatedAt", "FollowingId" },
                descending: new[] { false, false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_UserFollows_FollowingId_Status_CreatedAt_FollowerId",
                table: "UserFollows",
                columns: new[] { "FollowingId", "Status", "CreatedAt", "FollowerId" },
                descending: new[] { false, false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockedUserId_BlockerId",
                table: "UserBlocks",
                columns: new[] { "BlockedUserId", "BlockerId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockerId_CreatedAt_BlockedUserId",
                table: "UserBlocks",
                columns: new[] { "BlockerId", "CreatedAt", "BlockedUserId" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_UserId_CreatedAt_Id",
                table: "UserActivities",
                columns: new[] { "UserId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_SteamOwnedGames_UserId_PlaytimeMinutes_Name_SteamAppId",
                table: "SteamOwnedGames",
                columns: new[] { "UserId", "PlaytimeMinutes", "Name", "SteamAppId" },
                descending: new[] { false, true, false, false });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_IsRead_Priority_CreatedAtUtc_Id",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "IsRead", "Priority", "CreatedAtUtc", "Id" },
                descending: new[] { false, false, true, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_Type_CreatedAtUtc_Id",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "Type", "CreatedAtUtc", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxMessages_ProcessedAtUtc_DeadLetteredAtUtc_NextAttemptAtUtc_LockedUntilUtc_CreatedAtUtc",
                table: "NotificationOutboxMessages",
                columns: new[] { "ProcessedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_AverageRating_Id",
                table: "Games",
                columns: new[] { "AverageRating", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Name_Id",
                table: "Games",
                columns: new[] { "Name", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Games_PlayCount_Id",
                table: "Games",
                columns: new[] { "PlayCount", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Games_ReleaseDate_Name_Id",
                table: "Games",
                columns: new[] { "ReleaseDate", "Name", "Id" },
                descending: new[] { true, false, false },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GameLists_UserId_CreatedAt_Id",
                table: "GameLists",
                columns: new[] { "UserId", "CreatedAt", "Id" },
                descending: new[] { false, true, true })
                .Annotation("SqlServer:Include", new[] { "Visibility", "IsRanked", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPosts_IsDeleted_CreatedAt_Id",
                table: "UserPosts");

            migrationBuilder.DropIndex(
                name: "IX_UserPosts_UserId_CreatedAt_Id",
                table: "UserPosts");

            migrationBuilder.DropIndex(
                name: "IX_UserGames_Source_GameId",
                table: "UserGames");

            migrationBuilder.DropIndex(
                name: "IX_UserGames_UserId_UpdatedAt_CreatedAt_Id",
                table: "UserGames");

            migrationBuilder.DropIndex(
                name: "IX_UserFollows_FollowerId_Status_CreatedAt_FollowingId",
                table: "UserFollows");

            migrationBuilder.DropIndex(
                name: "IX_UserFollows_FollowingId_Status_CreatedAt_FollowerId",
                table: "UserFollows");

            migrationBuilder.DropIndex(
                name: "IX_UserBlocks_BlockedUserId_BlockerId",
                table: "UserBlocks");

            migrationBuilder.DropIndex(
                name: "IX_UserBlocks_BlockerId_CreatedAt_BlockedUserId",
                table: "UserBlocks");

            migrationBuilder.DropIndex(
                name: "IX_UserActivities_UserId_CreatedAt_Id",
                table: "UserActivities");

            migrationBuilder.DropIndex(
                name: "IX_SteamOwnedGames_UserId_PlaytimeMinutes_Name_SteamAppId",
                table: "SteamOwnedGames");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientUserId_IsRead_Priority_CreatedAtUtc_Id",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientUserId_Type_CreatedAtUtc_Id",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_NotificationOutboxMessages_ProcessedAtUtc_DeadLetteredAtUtc_NextAttemptAtUtc_LockedUntilUtc_CreatedAtUtc",
                table: "NotificationOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Games_AverageRating_Id",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_Name_Id",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_PlayCount_Id",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_ReleaseDate_Name_Id",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_GameLists_UserId_CreatedAt_Id",
                table: "GameLists");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAtUtc",
                table: "NotificationOutboxMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "ReviewComments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.CreateIndex(
                name: "IX_UserPosts_IsDeleted_CreatedAt",
                table: "UserPosts",
                columns: new[] { "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPosts_UserId_CreatedAt",
                table: "UserPosts",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserFollows_FollowingId",
                table: "UserFollows",
                column: "FollowingId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockedUserId",
                table: "UserBlocks",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_UserId_CreatedAt",
                table: "UserActivities",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SteamOwnedGames_UserId_PlaytimeMinutes",
                table: "SteamOwnedGames",
                columns: new[] { "UserId", "PlaytimeMinutes" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_IsRead_Priority_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "IsRead", "Priority", "CreatedAtUtc" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_Type_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "Type", "CreatedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxMessages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntilUtc_CreatedAtUtc",
                table: "NotificationOutboxMessages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });
        }
    }
}
