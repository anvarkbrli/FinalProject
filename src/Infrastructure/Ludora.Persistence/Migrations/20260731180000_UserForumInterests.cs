using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260731180000_UserForumInterests")]
    public partial class UserForumInterests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
            IF COL_LENGTH(N'dbo.AspNetUsers', N'ForumInterestsJson') IS NULL
            BEGIN
                ALTER TABLE [dbo].[AspNetUsers]
                ADD [ForumInterestsJson] nvarchar(max) NULL;
            END;
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
            IF COL_LENGTH(N'dbo.AspNetUsers', N'ForumInterestsJson') IS NOT NULL
            BEGIN
                ALTER TABLE [dbo].[AspNetUsers]
                DROP COLUMN [ForumInterestsJson];
            END;
            """);
        }
    }
}
