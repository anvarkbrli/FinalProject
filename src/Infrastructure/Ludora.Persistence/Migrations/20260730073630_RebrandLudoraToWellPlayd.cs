using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RebrandLudoraToWellPlayd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "BadgeDefinitions",
                keyColumn: "Id",
                keyValue: 28,
                column: "Description",
                value: "Steam hesabını WellPlayd-ə bağla.");

            migrationBuilder.UpdateData(
                table: "BadgeDefinitions",
                keyColumn: "Id",
                keyValue: 30,
                column: "Name",
                value: "WellPlayd Veteranı");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "BadgeDefinitions",
                keyColumn: "Id",
                keyValue: 28,
                column: "Description",
                value: "Steam hesabını Ludora-ya bağla.");

            migrationBuilder.UpdateData(
                table: "BadgeDefinitions",
                keyColumn: "Id",
                keyValue: 30,
                column: "Name",
                value: "Ludora Veteranı");
        }
    }
}
