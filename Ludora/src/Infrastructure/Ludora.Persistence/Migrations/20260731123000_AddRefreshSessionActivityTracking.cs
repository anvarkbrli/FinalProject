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
        migrationBuilder.AddColumn<DateTime>(
            name: "LastActiveAtUtc",
            table: "RefreshSessions",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<DateTime>(
            name: "SignedInAtUtc",
            table: "RefreshSessions",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.Sql(
            "UPDATE [RefreshSessions] SET [SignedInAtUtc] = [CreatedAtUtc], [LastActiveAtUtc] = [CreatedAtUtc]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastActiveAtUtc",
            table: "RefreshSessions");

        migrationBuilder.DropColumn(
            name: "SignedInAtUtc",
            table: "RefreshSessions");
    }
}
