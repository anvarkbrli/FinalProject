using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260801090000_RepairRefreshSessionSchema")]
public sealed class RepairRefreshSessionSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[RefreshSessions]
                (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [TokenHash] nvarchar(64) NOT NULL,
                    [SecurityStamp] nvarchar(128) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [SignedInAtUtc] datetime2 NOT NULL,
                    [LastActiveAtUtc] datetime2 NOT NULL,
                    [ExpiresAtUtc] datetime2 NOT NULL,
                    [CreatedByIp] nvarchar(64) NULL,
                    [UserAgent] nvarchar(512) NULL,
                    [RevokedAtUtc] datetime2 NULL,
                    [RevokedByIp] nvarchar(64) NULL,
                    [RevocationReason] nvarchar(200) NULL,
                    [ReplacedBySessionId] uniqueidentifier NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_RefreshSessions] PRIMARY KEY ([Id])
                );
            END;

            IF COL_LENGTH(N'dbo.RefreshSessions', N'SignedInAtUtc') IS NULL
            BEGIN
                ALTER TABLE [dbo].[RefreshSessions]
                ADD [SignedInAtUtc] datetime2 NULL;

                UPDATE [dbo].[RefreshSessions]
                SET [SignedInAtUtc] = [CreatedAtUtc]
                WHERE [SignedInAtUtc] IS NULL;

                ALTER TABLE [dbo].[RefreshSessions]
                ALTER COLUMN [SignedInAtUtc] datetime2 NOT NULL;
            END;

            IF COL_LENGTH(N'dbo.RefreshSessions', N'LastActiveAtUtc') IS NULL
            BEGIN
                ALTER TABLE [dbo].[RefreshSessions]
                ADD [LastActiveAtUtc] datetime2 NULL;

                UPDATE [dbo].[RefreshSessions]
                SET [LastActiveAtUtc] = [CreatedAtUtc]
                WHERE [LastActiveAtUtc] IS NULL;

                ALTER TABLE [dbo].[RefreshSessions]
                ALTER COLUMN [LastActiveAtUtc] datetime2 NOT NULL;
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.foreign_keys
                WHERE [name] = N'FK_RefreshSessions_AspNetUsers_UserId'
                  AND [parent_object_id] = OBJECT_ID(N'[dbo].[RefreshSessions]')
            )
            BEGIN
                ALTER TABLE [dbo].[RefreshSessions] WITH CHECK
                ADD CONSTRAINT [FK_RefreshSessions_AspNetUsers_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id])
                    ON DELETE CASCADE;

                ALTER TABLE [dbo].[RefreshSessions]
                CHECK CONSTRAINT [FK_RefreshSessions_AspNetUsers_UserId];
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_RefreshSessions_TokenHash'
                  AND [object_id] = OBJECT_ID(N'[dbo].[RefreshSessions]')
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_RefreshSessions_TokenHash]
                    ON [dbo].[RefreshSessions] ([TokenHash]);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE [name] = N'IX_RefreshSessions_UserId_ExpiresAtUtc'
                  AND [object_id] = OBJECT_ID(N'[dbo].[RefreshSessions]')
            )
            BEGIN
                CREATE INDEX [IX_RefreshSessions_UserId_ExpiresAtUtc]
                    ON [dbo].[RefreshSessions] ([UserId], [ExpiresAtUtc]);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This migration repairs potentially inconsistent development databases.
        // Rolling it back must not delete valid session data or the shared table.
    }
}
