using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    public partial class SteamIntegration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SteamAvatarUrl",
                table: "AspNetUsers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SteamDisplayName",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SteamId",
                table: "AspNetUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SteamLastSyncedAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SteamLinkedAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SteamProfileUrl",
                table: "AspNetUsers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SteamAppId",
                table: "Games",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SteamFriendSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FriendSteamId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FriendSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamFriendSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SteamFriendSnapshots_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SteamLinkChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StateHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamLinkChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SteamLinkChallenges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SteamOwnedGames",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SteamAppId = table.Column<int>(type: "int", nullable: false),
                    LocalGameId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PlaytimeMinutes = table.Column<int>(type: "int", nullable: false),
                    PlaytimeWindowsMinutes = table.Column<int>(type: "int", nullable: false),
                    PlaytimeMacMinutes = table.Column<int>(type: "int", nullable: false),
                    PlaytimeLinuxMinutes = table.Column<int>(type: "int", nullable: false),
                    PlaytimeTwoWeeksMinutes = table.Column<int>(type: "int", nullable: true),
                    LastPlayedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IconUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamOwnedGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SteamOwnedGames_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SteamOwnedGames_Games_LocalGameId",
                        column: x => x.LocalGameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserGamePlayLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    SteamAppId = table.Column<int>(type: "int", nullable: false),
                    PlaytimeMinutes = table.Column<int>(type: "int", nullable: false),
                    LastPlayedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGamePlayLogs", x => x.Id);
                    table.CheckConstraint("CK_UserGamePlayLogs_PlaytimeMinutes", "[PlaytimeMinutes] >= 0");
                    table.ForeignKey(
                        name: "FK_UserGamePlayLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGamePlayLogs_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SteamId",
                table: "AspNetUsers",
                column: "SteamId",
                unique: true,
                filter: "[SteamId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Games_SteamAppId",
                table: "Games",
                column: "SteamAppId",
                unique: true,
                filter: "[SteamAppId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SteamFriendSnapshots_UserId_FriendSteamId",
                table: "SteamFriendSnapshots",
                columns: new[] { "UserId", "FriendSteamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SteamFriendSnapshots_UserId_IsActive",
                table: "SteamFriendSnapshots",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SteamLinkChallenges_StateHash",
                table: "SteamLinkChallenges",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SteamLinkChallenges_UserId_ExpiresAtUtc",
                table: "SteamLinkChallenges",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SteamOwnedGames_LocalGameId",
                table: "SteamOwnedGames",
                column: "LocalGameId");

            migrationBuilder.CreateIndex(
                name: "IX_SteamOwnedGames_UserId_PlaytimeMinutes",
                table: "SteamOwnedGames",
                columns: new[] { "UserId", "PlaytimeMinutes" });

            migrationBuilder.CreateIndex(
                name: "IX_SteamOwnedGames_UserId_SteamAppId",
                table: "SteamOwnedGames",
                columns: new[] { "UserId", "SteamAppId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGamePlayLogs_GameId",
                table: "UserGamePlayLogs",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGamePlayLogs_UserId_GameId",
                table: "UserGamePlayLogs",
                columns: new[] { "UserId", "GameId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGamePlayLogs_UserId_SteamAppId",
                table: "UserGamePlayLogs",
                columns: new[] { "UserId", "SteamAppId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SteamFriendSnapshots");
            migrationBuilder.DropTable(name: "SteamLinkChallenges");
            migrationBuilder.DropTable(name: "SteamOwnedGames");
            migrationBuilder.DropTable(name: "UserGamePlayLogs");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SteamId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_Games_SteamAppId",
                table: "Games");

            migrationBuilder.DropColumn(name: "SteamAvatarUrl", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "SteamDisplayName", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "SteamId", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "SteamLastSyncedAtUtc", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "SteamLinkedAtUtc", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "SteamProfileUrl", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "SteamAppId", table: "Games");
        }
    }
}
