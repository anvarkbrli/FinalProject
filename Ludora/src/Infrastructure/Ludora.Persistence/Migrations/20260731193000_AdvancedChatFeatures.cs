using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260731193000_AdvancedChatFeatures")]
public partial class AdvancedChatFeatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'dbo.ChatConversations', N'CurrentStreak') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ChatConversations]
                ADD [CurrentStreak] int NOT NULL
                    CONSTRAINT [DF_ChatConversations_CurrentStreak] DEFAULT (0);
            END;

            IF COL_LENGTH(N'dbo.ChatConversations', N'LongestStreak') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ChatConversations]
                ADD [LongestStreak] int NOT NULL
                    CONSTRAINT [DF_ChatConversations_LongestStreak] DEFAULT (0);
            END;

            IF COL_LENGTH(N'dbo.ChatConversations', N'LastCompletedStreakDate') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ChatConversations]
                ADD [LastCompletedStreakDate] date NULL;
            END;

            IF COL_LENGTH(N'dbo.ChatMessages', N'ReplyToMessageId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ChatMessages]
                ADD [ReplyToMessageId] bigint NULL;
            END;
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.ChatConversationUserStates', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ChatConversationUserStates]
                (
                    [ConversationId] bigint NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [IsArchived] bit NOT NULL,
                    [ArchivedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_ChatConversationUserStates]
                        PRIMARY KEY ([ConversationId], [UserId]),
                    CONSTRAINT [FK_ChatConversationUserStates_AspNetUsers_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]),
                    CONSTRAINT [FK_ChatConversationUserStates_ChatConversations_ConversationId]
                        FOREIGN KEY ([ConversationId]) REFERENCES [dbo].[ChatConversations] ([Id])
                        ON DELETE CASCADE
                );
            END;

            IF OBJECT_ID(N'dbo.ChatMessageReactions', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ChatMessageReactions]
                (
                    [MessageId] bigint NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [ReactionType] int NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_ChatMessageReactions]
                        PRIMARY KEY ([MessageId], [UserId]),
                    CONSTRAINT [CK_ChatMessageReactions_ReactionType]
                        CHECK ([ReactionType] BETWEEN 0 AND 5),
                    CONSTRAINT [FK_ChatMessageReactions_AspNetUsers_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]),
                    CONSTRAINT [FK_ChatMessageReactions_ChatMessages_MessageId]
                        FOREIGN KEY ([MessageId]) REFERENCES [dbo].[ChatMessages] ([Id])
                        ON DELETE CASCADE
                );
            END;
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_ChatMessages_ConversationId_ReplyToMessageId'
                  AND [object_id] = OBJECT_ID(N'dbo.ChatMessages')
            )
            BEGIN
                CREATE INDEX [IX_ChatMessages_ConversationId_ReplyToMessageId]
                    ON [dbo].[ChatMessages] ([ConversationId], [ReplyToMessageId]);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_ChatMessages_ReplyToMessageId'
                  AND [object_id] = OBJECT_ID(N'dbo.ChatMessages')
            )
            BEGIN
                CREATE INDEX [IX_ChatMessages_ReplyToMessageId]
                    ON [dbo].[ChatMessages] ([ReplyToMessageId]);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_ChatConversationUserStates_UserId_IsArchived_ArchivedAtUtc'
                  AND [object_id] = OBJECT_ID(N'dbo.ChatConversationUserStates')
            )
            BEGIN
                CREATE INDEX [IX_ChatConversationUserStates_UserId_IsArchived_ArchivedAtUtc]
                    ON [dbo].[ChatConversationUserStates]
                    ([UserId], [IsArchived], [ArchivedAtUtc]);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_ChatMessageReactions_MessageId_ReactionType'
                  AND [object_id] = OBJECT_ID(N'dbo.ChatMessageReactions')
            )
            BEGIN
                CREATE INDEX [IX_ChatMessageReactions_MessageId_ReactionType]
                    ON [dbo].[ChatMessageReactions] ([MessageId], [ReactionType]);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_ChatMessageReactions_UserId_UpdatedAtUtc'
                  AND [object_id] = OBJECT_ID(N'dbo.ChatMessageReactions')
            )
            BEGIN
                CREATE INDEX [IX_ChatMessageReactions_UserId_UpdatedAtUtc]
                    ON [dbo].[ChatMessageReactions] ([UserId], [UpdatedAtUtc]);
            END;
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.foreign_keys
                WHERE [name] = N'FK_ChatMessages_ChatMessages_ReplyToMessageId'
            )
            BEGIN
                ALTER TABLE [dbo].[ChatMessages] WITH CHECK
                ADD CONSTRAINT [FK_ChatMessages_ChatMessages_ReplyToMessageId]
                    FOREIGN KEY ([ReplyToMessageId])
                    REFERENCES [dbo].[ChatMessages] ([Id]);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.check_constraints
                WHERE [name] = N'CK_ChatConversations_Streaks'
            )
            BEGIN
                ALTER TABLE [dbo].[ChatConversations] WITH CHECK
                ADD CONSTRAINT [CK_ChatConversations_Streaks]
                    CHECK ([CurrentStreak] >= 0 AND [LongestStreak] >= [CurrentStreak]);
            END;
            """);

        migrationBuilder.Sql(
            """
            ;WITH CompletedDays AS
            (
                SELECT
                    [ConversationId],
                    CAST(DATEADD(HOUR, 4, [SentAtUtc]) AS date) AS [CompletedDate]
                FROM [dbo].[ChatMessages]
                WHERE [DeletedForEveryoneAtUtc] IS NULL
                GROUP BY
                    [ConversationId],
                    CAST(DATEADD(HOUR, 4, [SentAtUtc]) AS date)
                HAVING COUNT(DISTINCT [SenderUserId]) >= 2
            ),
            NumberedDays AS
            (
                SELECT
                    [ConversationId],
                    [CompletedDate],
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY [ConversationId]
                        ORDER BY [CompletedDate]
                    ) AS [RowNumber]
                FROM CompletedDays
            ),
            Runs AS
            (
                SELECT
                    [ConversationId],
                    MAX([CompletedDate]) AS [EndDate],
                    COUNT(*) AS [RunLength]
                FROM NumberedDays
                GROUP BY
                    [ConversationId],
                    DATEADD(DAY, -CONVERT(int, [RowNumber]), [CompletedDate])
            ),
            LatestDays AS
            (
                SELECT
                    [ConversationId],
                    MAX([CompletedDate]) AS [LatestDate]
                FROM CompletedDays
                GROUP BY [ConversationId]
            ),
            CurrentRuns AS
            (
                SELECT r.[ConversationId], r.[RunLength]
                FROM Runs r
                INNER JOIN LatestDays l
                    ON l.[ConversationId] = r.[ConversationId]
                   AND l.[LatestDate] = r.[EndDate]
            ),
            LongestRuns AS
            (
                SELECT [ConversationId], MAX([RunLength]) AS [RunLength]
                FROM Runs
                GROUP BY [ConversationId]
            )
            UPDATE c
            SET
                c.[CurrentStreak] = ISNULL(cr.[RunLength], 0),
                c.[LongestStreak] = ISNULL(lr.[RunLength], 0),
                c.[LastCompletedStreakDate] = ld.[LatestDate]
            FROM [dbo].[ChatConversations] c
            LEFT JOIN LatestDays ld ON ld.[ConversationId] = c.[Id]
            LEFT JOIN CurrentRuns cr ON cr.[ConversationId] = c.[Id]
            LEFT JOIN LongestRuns lr ON lr.[ConversationId] = c.[Id];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS
            (
                SELECT 1 FROM sys.foreign_keys
                WHERE [name] = N'FK_ChatMessages_ChatMessages_ReplyToMessageId'
            )
            BEGIN
                ALTER TABLE [dbo].[ChatMessages]
                DROP CONSTRAINT [FK_ChatMessages_ChatMessages_ReplyToMessageId];
            END;

            IF EXISTS
            (
                SELECT 1 FROM sys.check_constraints
                WHERE [name] = N'CK_ChatConversations_Streaks'
            )
            BEGIN
                ALTER TABLE [dbo].[ChatConversations]
                DROP CONSTRAINT [CK_ChatConversations_Streaks];
            END;

            IF OBJECT_ID(N'dbo.ChatConversationUserStates', N'U') IS NOT NULL
                DROP TABLE [dbo].[ChatConversationUserStates];

            IF OBJECT_ID(N'dbo.ChatMessageReactions', N'U') IS NOT NULL
                DROP TABLE [dbo].[ChatMessageReactions];

            IF COL_LENGTH(N'dbo.ChatMessages', N'ReplyToMessageId') IS NOT NULL
                ALTER TABLE [dbo].[ChatMessages] DROP COLUMN [ReplyToMessageId];

            IF COL_LENGTH(N'dbo.ChatConversations', N'LastCompletedStreakDate') IS NOT NULL
                ALTER TABLE [dbo].[ChatConversations] DROP COLUMN [LastCompletedStreakDate];

            IF COL_LENGTH(N'dbo.ChatConversations', N'LongestStreak') IS NOT NULL
            BEGIN
                DECLARE @LongestDefault nvarchar(128);
                SELECT @LongestDefault = dc.[name]
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.[default_object_id] = dc.[object_id]
                WHERE dc.[parent_object_id] = OBJECT_ID(N'dbo.ChatConversations')
                  AND c.[name] = N'LongestStreak';

                IF @LongestDefault IS NOT NULL
                    EXEC(N'ALTER TABLE [dbo].[ChatConversations] DROP CONSTRAINT [' + @LongestDefault + N']');

                ALTER TABLE [dbo].[ChatConversations] DROP COLUMN [LongestStreak];
            END;

            IF COL_LENGTH(N'dbo.ChatConversations', N'CurrentStreak') IS NOT NULL
            BEGIN
                DECLARE @CurrentDefault nvarchar(128);
                SELECT @CurrentDefault = dc.[name]
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.[default_object_id] = dc.[object_id]
                WHERE dc.[parent_object_id] = OBJECT_ID(N'dbo.ChatConversations')
                  AND c.[name] = N'CurrentStreak';

                IF @CurrentDefault IS NOT NULL
                    EXEC(N'ALTER TABLE [dbo].[ChatConversations] DROP CONSTRAINT [' + @CurrentDefault + N']');

                ALTER TABLE [dbo].[ChatConversations] DROP COLUMN [CurrentStreak];
            END;
            """);
    }
}
