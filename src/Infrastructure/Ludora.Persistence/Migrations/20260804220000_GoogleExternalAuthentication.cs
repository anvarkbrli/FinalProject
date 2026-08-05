using System;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260804220000_GoogleExternalAuthentication")]
public partial class GoogleExternalAuthentication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExternalAuthTickets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                EmailVerified = table.Column<bool>(type: "bit", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                PictureUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReturnPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalAuthTickets", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExternalAuthTickets_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExternalAuthTickets_CodeHash",
            table: "ExternalAuthTickets",
            column: "CodeHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExternalAuthTickets_ExpiresAtUtc",
            table: "ExternalAuthTickets",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_ExternalAuthTickets_UserId",
            table: "ExternalAuthTickets",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ExternalAuthTickets");
    }
}
