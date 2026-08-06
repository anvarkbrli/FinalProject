using System;

using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260723120000_SteamPlaytimeReconciliation")]
    public partial class SteamPlaytimeReconciliation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserGamePlayLogs_Games_GameId",
                table: "UserGamePlayLogs");

            migrationBuilder.DropIndex(
                name: "IX_UserGamePlayLogs_GameId",
                table: "UserGamePlayLogs");

            migrationBuilder.DropIndex(
                name: "IX_UserGamePlayLogs_UserId_GameId",
                table: "UserGamePlayLogs");

            migrationBuilder.AddColumn<bool>(
                name: "IsSteamFallback",
                table: "Games",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<long>(
                name: "GameId",
                table: "UserGamePlayLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "GameSource",
                table: "UserGamePlayLogs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "InitialImportedPlaytimeMinutes",
                table: "UserGamePlayLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE [UserGamePlayLogs] SET [InitialImportedPlaytimeMinutes] = [PlaytimeMinutes]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserGamePlayLogs_InitialImportedPlaytimeMinutes",
                table: "UserGamePlayLogs",
                sql: "[InitialImportedPlaytimeMinutes] >= 0");

            migrationBuilder.AddColumn<int>(
                name: "MatchedGameSource",
                table: "SteamOwnedGames",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MatchedGameId",
                table: "SteamOwnedGames",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchedGameName",
                table: "SteamOwnedGames",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchedCoverImageUrl",
                table: "SteamOwnedGames",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MatchedReleaseDate",
                table: "SteamOwnedGames",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchMethod",
                table: "SteamOwnedGames",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MatchConfidence",
                table: "SteamOwnedGames",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsSteamFallback",
                table: "SteamOwnedGames",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PlaytimeResolution",
                table: "SteamOwnedGames",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManualPlaytimeSnapshotMinutes",
                table: "SteamOwnedGames",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManualPlatformSourceSnapshot",
                table: "SteamOwnedGames",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ManualPlatformIdSnapshot",
                table: "SteamOwnedGames",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ManualPlayedOnSnapshot",
                table: "SteamOwnedGames",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvedPlaytimeMinutes",
                table: "SteamOwnedGames",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionConfirmedAtUtc",
                table: "SteamOwnedGames",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(@"
            UPDATE sog
            SET [MatchedGameSource] = 1,
                [MatchedGameId] = sog.[LocalGameId],
                [MatchedGameName] = g.[Name],
                [MatchedCoverImageUrl] = g.[CoverImageUrl],
                [MatchedReleaseDate] = g.[ReleaseDate],
                [MatchMethod] = 1,
                [MatchConfidence] = 1
            FROM [SteamOwnedGames] sog
            INNER JOIN [Games] g ON g.[Id] = sog.[LocalGameId]
            WHERE sog.[LocalGameId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SteamOwnedGames_MatchConfidence",
                table: "SteamOwnedGames",
                sql: "[MatchConfidence] >= 0 AND [MatchConfidence] <= 1");

            migrationBuilder.CreateTable(
                name: "SteamPlaytimeDeltas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserGamePlayLogId = table.Column<long>(type: "bigint", nullable: false),
                    DeltaMinutes = table.Column<int>(type: "int", nullable: false),
                    TotalPlaytimeMinutes = table.Column<int>(type: "int", nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamPlaytimeDeltas", x => x.Id);
                    table.CheckConstraint(
                        "CK_SteamPlaytimeDeltas_DeltaMinutes",
                        "[DeltaMinutes] > 0");
                    table.CheckConstraint(
                        "CK_SteamPlaytimeDeltas_TotalPlaytimeMinutes",
                        "[TotalPlaytimeMinutes] >= [DeltaMinutes]");
                    table.ForeignKey(
                        name: "FK_SteamPlaytimeDeltas_UserGamePlayLogs_UserGamePlayLogId",
                        column: x => x.UserGamePlayLogId,
                        principalTable: "UserGamePlayLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserGamePlayLogs_UserId_GameSource_GameId",
                table: "UserGamePlayLogs",
                columns: new[] { "UserId", "GameSource", "GameId" });

            migrationBuilder.CreateIndex(
                name: "IX_SteamOwnedGames_UserId_MatchedGameSource_MatchedGameId",
                table: "SteamOwnedGames",
                columns: new[] { "UserId", "MatchedGameSource", "MatchedGameId" });

            migrationBuilder.CreateIndex(
                name: "IX_SteamOwnedGames_UserId_PlaytimeResolution",
                table: "SteamOwnedGames",
                columns: new[] { "UserId", "PlaytimeResolution" });

            migrationBuilder.CreateIndex(
                name: "IX_SteamPlaytimeDeltas_OccurredOn",
                table: "SteamPlaytimeDeltas",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_SteamPlaytimeDeltas_UserGamePlayLogId_SyncedAtUtc",
                table: "SteamPlaytimeDeltas",
                columns: new[] { "UserGamePlayLogId", "SyncedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SteamPlaytimeDeltas");

            migrationBuilder.DropIndex(
                name: "IX_UserGamePlayLogs_UserId_GameSource_GameId",
                table: "UserGamePlayLogs");
            migrationBuilder.DropIndex(
                name: "IX_SteamOwnedGames_UserId_MatchedGameSource_MatchedGameId",
                table: "SteamOwnedGames");
            migrationBuilder.DropIndex(
                name: "IX_SteamOwnedGames_UserId_PlaytimeResolution",
                table: "SteamOwnedGames");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserGamePlayLogs_InitialImportedPlaytimeMinutes",
                table: "UserGamePlayLogs");
            migrationBuilder.DropCheckConstraint(
                name: "CK_SteamOwnedGames_MatchConfidence",
                table: "SteamOwnedGames");

            string[] steamOwnedColumns =
            {
                "MatchedGameSource", "MatchedGameId", "MatchedGameName",
                "MatchedCoverImageUrl", "MatchedReleaseDate", "MatchMethod",
                "MatchConfidence", "IsSteamFallback", "PlaytimeResolution",
                "ManualPlaytimeSnapshotMinutes", "ManualPlatformSourceSnapshot",
                "ManualPlatformIdSnapshot", "ManualPlayedOnSnapshot",
                "ResolvedPlaytimeMinutes", "ResolutionConfirmedAtUtc"
            };
            foreach (var column in steamOwnedColumns)
                migrationBuilder.DropColumn(name: column, table: "SteamOwnedGames");

            // The previous model only supported local integer game references.
            migrationBuilder.Sql(
                "DELETE FROM [UserGamePlayLogs] WHERE [GameSource] <> 1 OR [GameId] > 2147483647");

            migrationBuilder.DropColumn(name: "GameSource", table: "UserGamePlayLogs");
            migrationBuilder.DropColumn(name: "InitialImportedPlaytimeMinutes", table: "UserGamePlayLogs");
            migrationBuilder.DropColumn(name: "IsSteamFallback", table: "Games");

            migrationBuilder.AlterColumn<int>(
                name: "GameId",
                table: "UserGamePlayLogs",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_UserGamePlayLogs_GameId",
                table: "UserGamePlayLogs",
                column: "GameId");
            migrationBuilder.CreateIndex(
                name: "IX_UserGamePlayLogs_UserId_GameId",
                table: "UserGamePlayLogs",
                columns: new[] { "UserId", "GameId" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserGamePlayLogs_Games_GameId",
                table: "UserGamePlayLogs",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
