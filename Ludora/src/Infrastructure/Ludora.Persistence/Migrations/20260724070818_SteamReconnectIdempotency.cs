using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SteamReconnectIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserGamePlayLogs_UserId_SteamAppId",
                table: "UserGamePlayLogs");

            migrationBuilder.AddColumn<string>(
                name: "SteamAccountId",
                table: "UserGamePlayLogs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE logs
                SET logs.[SteamAccountId] =
                    COALESCE(NULLIF(users.[SteamId], N''), N'legacy')
                FROM [UserGamePlayLogs] AS logs
                INNER JOIN [AspNetUsers] AS users
                    ON users.[Id] = logs.[UserId];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserGamePlayLogs_UserId_SteamAccountId_SteamAppId",
                table: "UserGamePlayLogs",
                columns: new[] { "UserId", "SteamAccountId", "SteamAppId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserGamePlayLogs_UserId_SteamAccountId_SteamAppId",
                table: "UserGamePlayLogs");

            migrationBuilder.Sql(
                """
                WITH duplicateLogs AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [UserId], [SteamAppId]
                               ORDER BY [LastSyncedAtUtc] DESC, [Id] DESC
                           ) AS rowNumber
                    FROM [UserGamePlayLogs]
                )
                DELETE FROM duplicateLogs
                WHERE rowNumber > 1;
                """);

            migrationBuilder.DropColumn(
                name: "SteamAccountId",
                table: "UserGamePlayLogs");

            migrationBuilder.CreateIndex(
                name: "IX_UserGamePlayLogs_UserId_SteamAppId",
                table: "UserGamePlayLogs",
                columns: new[] { "UserId", "SteamAppId" },
                unique: true);
        }
    }
}
