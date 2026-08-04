using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260804113000_ContactSupportInbox")]
public partial class ContactSupportInbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ContactMessageId",
            table: "EmailOutboxMessages",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EmailSubject",
            table: "EmailOutboxMessages",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "HtmlBody",
            table: "EmailOutboxMessages",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlainTextBody",
            table: "EmailOutboxMessages",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ContactMessages",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Message = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                Language = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RepliedByAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReplySubject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                ReplyBody = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                ReplyQueuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RepliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContactMessages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EmailOutboxMessages_ContactMessageId",
            table: "EmailOutboxMessages",
            column: "ContactMessageId");

        migrationBuilder.CreateIndex(
            name: "IX_ContactMessages_Email",
            table: "ContactMessages",
            column: "Email");

        migrationBuilder.CreateIndex(
            name: "IX_ContactMessages_Status_CreatedAtUtc",
            table: "ContactMessages",
            columns: new[] { "Status", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ContactMessages_UserId",
            table: "ContactMessages",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ContactMessages");

        migrationBuilder.DropIndex(
            name: "IX_EmailOutboxMessages_ContactMessageId",
            table: "EmailOutboxMessages");

        migrationBuilder.DropColumn(
            name: "ContactMessageId",
            table: "EmailOutboxMessages");

        migrationBuilder.DropColumn(
            name: "EmailSubject",
            table: "EmailOutboxMessages");

        migrationBuilder.DropColumn(
            name: "HtmlBody",
            table: "EmailOutboxMessages");

        migrationBuilder.DropColumn(
            name: "PlainTextBody",
            table: "EmailOutboxMessages");
    }
}
