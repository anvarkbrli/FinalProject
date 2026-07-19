using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260719213000_ReviewPlatformAndPlaytime")]
public partial class ReviewPlatformAndPlaytime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "PlatformId",
            table: "Reviews",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PlatformSource",
            table: "Reviews",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PlaytimeMinutes",
            table: "Reviews",
            type: "int",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Reviews_PlatformReference",
            table: "Reviews",
            sql: "([PlatformSource] IS NULL AND [PlatformId] IS NULL) OR ([PlatformSource] IN (1, 2) AND [PlatformId] > 0 AND ([PlatformSource] <> 1 OR [PlatformId] <= 2147483647))");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Reviews_PlaytimeMinutes",
            table: "Reviews",
            sql: "[PlaytimeMinutes] IS NULL OR ([PlaytimeMinutes] >= 0 AND [PlaytimeMinutes] <= 600000)");

        migrationBuilder.CreateIndex(
            name: "IX_Reviews_PlatformSource_PlatformId",
            table: "Reviews",
            columns: new[] { "PlatformSource", "PlatformId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Reviews_PlatformSource_PlatformId",
            table: "Reviews");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Reviews_PlatformReference",
            table: "Reviews");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Reviews_PlaytimeMinutes",
            table: "Reviews");

        migrationBuilder.DropColumn(
            name: "PlatformId",
            table: "Reviews");

        migrationBuilder.DropColumn(
            name: "PlatformSource",
            table: "Reviews");

        migrationBuilder.DropColumn(
            name: "PlaytimeMinutes",
            table: "Reviews");
    }
}
