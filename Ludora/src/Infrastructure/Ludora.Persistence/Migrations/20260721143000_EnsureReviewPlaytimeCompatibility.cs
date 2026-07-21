using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260721143000_EnsureReviewPlaytimeCompatibility")]
public partial class EnsureReviewPlaytimeCompatibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'dbo.Reviews', N'PlaytimeMinutes') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Reviews]
                ADD [PlaytimeMinutes] int NULL;
            END;

            IF OBJECT_ID(N'[dbo].[CK_Reviews_PlaytimeMinutes]', N'C') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Reviews] WITH CHECK
                ADD CONSTRAINT [CK_Reviews_PlaytimeMinutes]
                CHECK ([PlaytimeMinutes] IS NULL OR ([PlaytimeMinutes] >= 0 AND [PlaytimeMinutes] <= 600000));
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Uyğunluq migration-u mövcud istehsal sütununu geri silmir.
    }
}
