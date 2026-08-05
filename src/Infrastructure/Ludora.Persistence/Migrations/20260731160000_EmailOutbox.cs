using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260731160000_EmailOutbox")]
public partial class EmailOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EmailOutboxMessages",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TemplateType = table.Column<int>(type: "int", nullable: false),
                RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                ActionUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeadLetteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                LockToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmailOutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EmailOutboxMessages_ProcessedAtUtc_DeadLetteredAtUtc_NextAttemptAtUtc_LockedUntilUtc_CreatedAtUtc",
            table: "EmailOutboxMessages",
            columns: new[]
            {
                "ProcessedAtUtc",
                "DeadLetteredAtUtc",
                "NextAttemptAtUtc",
                "LockedUntilUtc",
                "CreatedAtUtc"
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EmailOutboxMessages");
    }
}
