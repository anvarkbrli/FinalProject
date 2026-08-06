using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260802110000_ChatMessageMediaCollections")]
    public partial class ChatMessageMediaCollections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
            IF OBJECT_ID(N'dbo.ChatMessageMedia', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ChatMessageMedia]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL,
                    [MessageId] bigint NOT NULL,
                    [StorageKey] nvarchar(512) NOT NULL,
                    [ContentType] nvarchar(100) NOT NULL,
                    [OriginalFileName] nvarchar(255) NOT NULL,
                    [Length] bigint NOT NULL,
                    [SortOrder] int NOT NULL,
                    CONSTRAINT [PK_ChatMessageMedia] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ChatMessageMedia_ChatMessages_MessageId]
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
                WHERE [name] = N'IX_ChatMessageMedia_MessageId_SortOrder'
                  AND [object_id] = OBJECT_ID(N'dbo.ChatMessageMedia')
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_ChatMessageMedia_MessageId_SortOrder]
                    ON [dbo].[ChatMessageMedia] ([MessageId], [SortOrder]);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_ChatMessageMedia_StorageKey'
                  AND [object_id] = OBJECT_ID(N'dbo.ChatMessageMedia')
            )
            BEGIN
                CREATE INDEX [IX_ChatMessageMedia_StorageKey]
                    ON [dbo].[ChatMessageMedia] ([StorageKey]);
            END;
            """);

            migrationBuilder.Sql(
                """
            INSERT INTO [dbo].[ChatMessageMedia]
                ([MessageId], [StorageKey], [ContentType], [OriginalFileName], [Length], [SortOrder])
            SELECT
                message.[Id],
                message.[MediaStorageKey],
                COALESCE(NULLIF(message.[MediaContentType], N''), N'application/octet-stream'),
                N'media',
                COALESCE(message.[MediaLength], 0),
                0
            FROM [dbo].[ChatMessages] message
            WHERE message.[MediaStorageKey] IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [dbo].[ChatMessageMedia] media
                  WHERE media.[MessageId] = message.[Id]
              );
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
            IF OBJECT_ID(N'dbo.ChatMessageMedia', N'U') IS NOT NULL
                DROP TABLE [dbo].[ChatMessageMedia];
            """);
        }
    }
}
