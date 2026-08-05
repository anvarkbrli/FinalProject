using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewHardwareSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HardwareCpuName",
                table: "Reviews",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HardwareGpuName",
                table: "Reviews",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardwareRamGb",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardwareVramGb",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowHardwareProfile",
                table: "Reviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_HardwareRamGb",
                table: "Reviews",
                sql: "[HardwareRamGb] IS NULL OR ([HardwareRamGb] >= 1 AND [HardwareRamGb] <= 512)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_HardwareVramGb",
                table: "Reviews",
                sql: "[HardwareVramGb] IS NULL OR ([HardwareVramGb] >= 1 AND [HardwareVramGb] <= 128)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Reviews_HardwareRamGb",
                table: "Reviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reviews_HardwareVramGb",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HardwareCpuName",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HardwareGpuName",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HardwareRamGb",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HardwareVramGb",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ShowHardwareProfile",
                table: "Reviews");
        }
    }
}
