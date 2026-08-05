using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260805093000_GroupMembershipVisibilityAndSystemMessages")]
public partial class GroupMembershipVisibilityAndSystemMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ChatMessages_Kind",
            table: "ChatMessages");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ChatMessages_Kind",
            table: "ChatMessages",
            sql: "[Kind] BETWEEN 0 AND 4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [ChatMessages] WHERE [Kind] = 4)
                THROW 51001, 'Rollback blocked: group system messages exist. Restore a pre-migration backup instead.', 1;
            """);

        migrationBuilder.DropCheckConstraint(
            name: "CK_ChatMessages_Kind",
            table: "ChatMessages");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ChatMessages_Kind",
            table: "ChatMessages",
            sql: "[Kind] BETWEEN 0 AND 3");
    }
}
