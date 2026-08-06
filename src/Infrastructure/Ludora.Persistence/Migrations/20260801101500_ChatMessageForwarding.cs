using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260801101500_ChatMessageForwarding")]
    public partial class ChatMessageForwarding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
            IF COL_LENGTH(N'dbo.ChatMessages', N'IsForwarded') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ChatMessages]
                ADD [IsForwarded] bit NOT NULL
                    CONSTRAINT [DF_ChatMessages_IsForwarded] DEFAULT (0);
            END;
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
            IF COL_LENGTH(N'dbo.ChatMessages', N'IsForwarded') IS NOT NULL
            BEGIN
                DECLARE @constraintName sysname;
                SELECT @constraintName = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'dbo.ChatMessages')
                  AND c.name = N'IsForwarded';

                IF @constraintName IS NOT NULL
                    EXEC(N'ALTER TABLE [dbo].[ChatMessages] DROP CONSTRAINT [' + @constraintName + N']');

                ALTER TABLE [dbo].[ChatMessages]
                DROP COLUMN [IsForwarded];
            END;
            """);
        }
    }
}
