using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260721190000_BackfillTrackingLogs")]
public partial class BackfillTrackingLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE reviewLog
            SET
                [Content] = N'',
                [ContainsSpoilers] = 0,
                [PlayedOn] = COALESCE(
                    reviewLog.[PlayedOn],
                    CONVERT(date, DATEADD(hour, 4, COALESCE(userGame.[UpdatedAt], userGame.[CreatedAt])))),
                [CreatedAt] = COALESCE(userGame.[UpdatedAt], userGame.[CreatedAt]),
                [UpdatedAt] = NULL,
                [IsDeleted] = 0,
                [DeletedAtUtc] = NULL
            FROM [Reviews] AS reviewLog
            INNER JOIN [UserGames] AS userGame
                ON reviewLog.[UserId] = userGame.[UserId]
                AND reviewLog.[Source] = userGame.[Source]
                AND reviewLog.[GameId] = userGame.[GameId]
            WHERE
                reviewLog.[IsDeleted] = 1
                AND (userGame.[Status] IS NOT NULL OR userGame.[Rating] IS NOT NULL);

            INSERT INTO [Reviews]
            (
                [UserId],
                [Source],
                [GameId],
                [Content],
                [ContainsSpoilers],
                [PlayedOn],
                [PlatformSource],
                [PlatformId],
                [PlaytimeMinutes],
                [CreatedAt],
                [UpdatedAt],
                [IsDeleted],
                [DeletedAtUtc]
            )
            SELECT
                userGame.[UserId],
                userGame.[Source],
                userGame.[GameId],
                N'',
                0,
                CONVERT(date, DATEADD(hour, 4, COALESCE(userGame.[UpdatedAt], userGame.[CreatedAt]))),
                NULL,
                NULL,
                NULL,
                COALESCE(userGame.[UpdatedAt], userGame.[CreatedAt]),
                NULL,
                0,
                NULL
            FROM [UserGames] AS userGame
            WHERE
                (userGame.[Status] IS NOT NULL OR userGame.[Rating] IS NOT NULL)
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM [Reviews] AS reviewLog
                    WHERE
                        reviewLog.[UserId] = userGame.[UserId]
                        AND reviewLog.[Source] = userGame.[Source]
                        AND reviewLog.[GameId] = userGame.[GameId]
                );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data uyğunluğu üçün yaradılan boş loglar geri silinmir.
    }
}
