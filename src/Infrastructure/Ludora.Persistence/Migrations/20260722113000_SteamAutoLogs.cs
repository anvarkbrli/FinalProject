using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260722113000_SteamAutoLogs")]
public partial class SteamAutoLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsSteamAutoLogged",
            table: "Reviews",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsSteamPlayedOnOverridden",
            table: "Reviews",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsSteamPlaytimeOverridden",
            table: "Reviews",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsSteamAutoLogged",
            table: "Reviews");

        migrationBuilder.DropColumn(
            name: "IsSteamPlayedOnOverridden",
            table: "Reviews");

        migrationBuilder.DropColumn(
            name: "IsSteamPlaytimeOverridden",
            table: "Reviews");
    }
}
