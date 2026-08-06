using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260731123000_AddRefreshSessionActivityTracking")]
    public partial class AddRefreshSessionActivityTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep each schema change in a separate SQL batch. SQL Server compiles a
            // batch before executing it, so adding and then referencing a new column
            // in the same batch can fail with "Invalid column name".
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.RefreshSessions', N'SignedInAtUtc') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[RefreshSessions]
                    ADD [SignedInAtUtc] datetime2 NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.RefreshSessions', N'SignedInAtUtc') IS NOT NULL
                BEGIN
                    UPDATE [dbo].[RefreshSessions]
                    SET [SignedInAtUtc] = [CreatedAtUtc]
                    WHERE [SignedInAtUtc] IS NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
                   AND EXISTS
                   (
                       SELECT 1
                       FROM sys.columns
                       WHERE [object_id] = OBJECT_ID(N'[dbo].[RefreshSessions]')
                         AND [name] = N'SignedInAtUtc'
                         AND [is_nullable] = 1
                   )
                BEGIN
                    ALTER TABLE [dbo].[RefreshSessions]
                    ALTER COLUMN [SignedInAtUtc] datetime2 NOT NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.RefreshSessions', N'LastActiveAtUtc') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[RefreshSessions]
                    ADD [LastActiveAtUtc] datetime2 NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.RefreshSessions', N'LastActiveAtUtc') IS NOT NULL
                BEGIN
                    UPDATE [dbo].[RefreshSessions]
                    SET [LastActiveAtUtc] = [CreatedAtUtc]
                    WHERE [LastActiveAtUtc] IS NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
                   AND EXISTS
                   (
                       SELECT 1
                       FROM sys.columns
                       WHERE [object_id] = OBJECT_ID(N'[dbo].[RefreshSessions]')
                         AND [name] = N'LastActiveAtUtc'
                         AND [is_nullable] = 1
                   )
                BEGIN
                    ALTER TABLE [dbo].[RefreshSessions]
                    ALTER COLUMN [LastActiveAtUtc] datetime2 NOT NULL;
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.RefreshSessions', N'LastActiveAtUtc') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[RefreshSessions]
                    DROP COLUMN [LastActiveAtUtc];
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RefreshSessions]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.RefreshSessions', N'SignedInAtUtc') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[RefreshSessions]
                    DROP COLUMN [SignedInAtUtc];
                END;
                """);
        }
    }
}
