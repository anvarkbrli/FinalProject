using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260803074500_GameCatalogFilterPerformance")]
public partial class GameCatalogFilterPerformance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Games_GameType_Name_Id",
            table: "Games",
            columns: new[] { "GameType", "Name", "Id" },
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Games_GameType_ReleaseDate_Name_Id",
            table: "Games",
            columns: new[] { "GameType", "ReleaseDate", "Name", "Id" },
            descending: new[] { false, true, false, false },
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Games_GameType_Name_Id",
            table: "Games");

        migrationBuilder.DropIndex(
            name: "IX_Games_GameType_ReleaseDate_Name_Id",
            table: "Games");
    }
}
