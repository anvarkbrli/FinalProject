using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Changes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "UserGames",
                type: "float",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserGames_Rating",
                table: "UserGames",
                sql: "[Rating] IS NULL OR ([Rating] >= 0.5 AND [Rating] <= 5 AND [Rating] * 2 = FLOOR([Rating] * 2))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserGames_Rating",
                table: "UserGames");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "UserGames");
        }
    }
}
