using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

public partial class UserProfileCollectionsAndJournal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserActivities",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                GameSource = table.Column<int>(type: "int", nullable: true),
                GameId = table.Column<long>(type: "bigint", nullable: true),
                ReviewId = table.Column<long>(type: "bigint", nullable: true),
                Status = table.Column<int>(type: "int", nullable: true),
                Rating = table.Column<double>(type: "float", nullable: true),
                PostId = table.Column<long>(type: "bigint", nullable: true),
                GameListId = table.Column<int>(type: "int", nullable: true),
                SubjectTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserActivities", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserActivities_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserActivities_UserId_CreatedAt",
            table: "UserActivities",
            columns: new[] { "UserId", "CreatedAt" });

        migrationBuilder.Sql(
            """
            INSERT INTO [UserActivities]
                ([UserId], [Type], [GameSource], [GameId], [Status], [CreatedAt])
            SELECT
                [UserId], 1, [Source], [GameId], [Status], COALESCE([UpdatedAt], [CreatedAt])
            FROM [UserGames]
            WHERE [Status] IS NOT NULL;

            INSERT INTO [UserActivities]
                ([UserId], [Type], [GameSource], [GameId], [CreatedAt])
            SELECT
                [UserId], 3, [Source], [GameId], COALESCE([UpdatedAt], [CreatedAt])
            FROM [UserGames]
            WHERE [IsFavorite] = 1;

            INSERT INTO [UserActivities]
                ([UserId], [Type], [GameSource], [GameId], [CreatedAt])
            SELECT
                [UserId], 5, [Source], [GameId], COALESCE([UpdatedAt], [CreatedAt])
            FROM [UserGames]
            WHERE [IsWishlisted] = 1;

            INSERT INTO [UserActivities]
                ([UserId], [Type], [GameSource], [GameId], [Rating], [CreatedAt])
            SELECT
                [UserId], 7, [Source], [GameId], [Rating], COALESCE([UpdatedAt], [CreatedAt])
            FROM [UserGames]
            WHERE [Rating] IS NOT NULL;

            INSERT INTO [UserActivities]
                ([UserId], [Type], [GameSource], [GameId], [ReviewId], [CreatedAt])
            SELECT
                [UserId], 9, [Source], [GameId], [Id], [CreatedAt]
            FROM [Reviews]
            WHERE [IsDeleted] = 0;

            INSERT INTO [UserActivities]
                ([UserId], [Type], [TargetUserId], [GameSource], [GameId], [ReviewId], [CreatedAt])
            SELECT
                rl.[UserId], 12, r.[UserId], r.[Source], r.[GameId], r.[Id], rl.[CreatedAt]
            FROM [ReviewLikes] rl
            INNER JOIN [Reviews] r ON r.[Id] = rl.[ReviewId]
            WHERE r.[IsDeleted] = 0;

            INSERT INTO [UserActivities]
                ([UserId], [Type], [TargetUserId], [GameSource], [GameId], [ReviewId], [CreatedAt])
            SELECT
                rc.[UserId], 14, r.[UserId], r.[Source], r.[GameId], r.[Id], rc.[CreatedAt]
            FROM [ReviewComments] rc
            INNER JOIN [Reviews] r ON r.[Id] = rc.[ReviewId]
            WHERE rc.[IsDeleted] = 0 AND r.[IsDeleted] = 0;

            INSERT INTO [UserActivities]
                ([UserId], [Type], [TargetUserId], [CreatedAt])
            SELECT
                [FollowerId],
                CASE WHEN [Status] = 2 THEN 18 ELSE 17 END,
                [FollowingId],
                COALESCE([AcceptedAt], [CreatedAt])
            FROM [UserFollows];

            INSERT INTO [UserActivities]
                ([UserId], [Type], [TargetUserId], [CreatedAt])
            SELECT
                [BlockerId], 21, [BlockedUserId], [CreatedAt]
            FROM [UserBlocks];

            INSERT INTO [UserActivities]
                ([UserId], [Type], [PostId], [CreatedAt])
            SELECT
                [UserId], 23, [Id], [CreatedAt]
            FROM [UserPosts]
            WHERE [IsDeleted] = 0;

            INSERT INTO [UserActivities]
                ([UserId], [Type], [GameListId], [SubjectTitle], [CreatedAt])
            SELECT
                [UserId], 26, [Id], [Name], [CreatedAt]
            FROM [GameLists]
            WHERE [IsDeleted] = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserActivities");
    }
}
