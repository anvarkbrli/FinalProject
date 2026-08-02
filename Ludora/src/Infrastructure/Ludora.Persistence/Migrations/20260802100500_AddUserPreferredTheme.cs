using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260802100500_AddUserPreferredTheme")]
public sealed class AddUserPreferredTheme : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'dbo.AspNetUsers', N'PreferredTheme') IS NULL
            BEGIN
                ALTER TABLE [dbo].[AspNetUsers]
                ADD [PreferredTheme] nvarchar(10) NOT NULL
                    CONSTRAINT [DF_AspNetUsers_PreferredTheme] DEFAULT(N'dark');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'dbo.AspNetUsers', N'PreferredTheme') IS NOT NULL
            BEGIN
                DECLARE @DefaultConstraintName sysname;

                SELECT @DefaultConstraintName = dc.[name]
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'dbo.AspNetUsers')
                  AND c.[name] = N'PreferredTheme';

                IF @DefaultConstraintName IS NOT NULL
                    EXEC(N'ALTER TABLE [dbo].[AspNetUsers] DROP CONSTRAINT ['
                        + @DefaultConstraintName + N']');

                ALTER TABLE [dbo].[AspNetUsers]
                DROP COLUMN [PreferredTheme];
            END;
            """);
    }
}
