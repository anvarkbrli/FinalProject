using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260731123000_AddRefreshSessionActivityTracking")]
public partial class AddRefreshSessionActivityTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Some development databases had the migration history entry for the
        // refresh-session table without the actual table. Do not fail here;
        // the later RepairRefreshSessionSchema migration restores the full schema.
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
            BEGIN
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
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.RefreshSessions', N'LastActiveAtUtc') IS NOT NULL
                    ALTER TABLE [dbo].[RefreshSessions] DROP COLUMN [LastActiveAtUtc];

                IF COL_LENGTH(N'dbo.RefreshSessions', N'SignedInAtUtc') IS NOT NULL
                    ALTER TABLE [dbo].[RefreshSessions] DROP COLUMN [SignedInAtUtc];
            END;
            """);
    }
}
