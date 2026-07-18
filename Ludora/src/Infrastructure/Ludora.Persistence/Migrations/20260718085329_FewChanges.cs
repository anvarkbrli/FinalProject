using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FewChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameType",
                table: "IgdbGameOverrides",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GameType",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GameRelations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseGameSource = table.Column<int>(type: "int", nullable: false),
                    BaseGameId = table.Column<long>(type: "bigint", nullable: false),
                    RelatedGameSource = table.Column<int>(type: "int", nullable: false),
                    RelatedGameId = table.Column<long>(type: "bigint", nullable: false),
                    RelationType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameRelations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameRelations_BaseGameSource_BaseGameId",
                table: "GameRelations",
                columns: new[] { "BaseGameSource", "BaseGameId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameRelations_BaseGameSource_BaseGameId_RelatedGameSource_RelatedGameId_RelationType",
                table: "GameRelations",
                columns: new[] { "BaseGameSource", "BaseGameId", "RelatedGameSource", "RelatedGameId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameRelations_RelatedGameSource_RelatedGameId",
                table: "GameRelations",
                columns: new[] { "RelatedGameSource", "RelatedGameId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameRelations");

            migrationBuilder.DropColumn(
                name: "GameType",
                table: "IgdbGameOverrides");

            migrationBuilder.DropColumn(
                name: "GameType",
                table: "Games");
        }
    }
}
