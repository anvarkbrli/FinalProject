using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IGDBIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameGenres_Genres_GenreId",
                table: "GameGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_GameListItems_Games_GameId",
                table: "GameListItems");

            migrationBuilder.DropForeignKey(
                name: "FK_GamePlatforms_Platforms_PlatformId",
                table: "GamePlatforms");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Companies_DeveloperCompanyId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_DeveloperCompanyId",
                table: "Games");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GamePlatforms",
                table: "GamePlatforms");

            migrationBuilder.DropIndex(
                name: "IX_GamePlatforms_PlatformId",
                table: "GamePlatforms");

            migrationBuilder.DropIndex(
                name: "IX_GameLists_UserId_Name",
                table: "GameLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameListItems",
                table: "GameListItems");

            migrationBuilder.DropIndex(
                name: "IX_GameListItems_GameId",
                table: "GameListItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameGenres",
                table: "GameGenres");

            migrationBuilder.DropIndex(
                name: "IX_GameGenres_GenreId",
                table: "GameGenres");

            migrationBuilder.DropColumn(
                name: "DeveloperCompanyId",
                table: "Games");

            migrationBuilder.RenameColumn(
                name: "PlatformId",
                table: "GamePlatforms",
                newName: "PlatformSource");

            migrationBuilder.RenameColumn(
                name: "GameId",
                table: "GameListItems",
                newName: "GameSource");

            migrationBuilder.RenameColumn(
                name: "GenreId",
                table: "GameGenres",
                newName: "GenreSource");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Platforms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Genres",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ReleaseDate",
                table: "Games",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Games",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Games",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CoverImageUrl",
                table: "Games",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<long>(
                name: "PlatformSourceId",
                table: "GamePlatforms",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "GameSourceId",
                table: "GameListItems",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "GameListItems",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameTitle",
                table: "GameListItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "GameListItems",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleaseDate",
                table: "GameListItems",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GenreSourceId",
                table: "GameGenres",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Companies",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GamePlatforms",
                table: "GamePlatforms",
                columns: new[] { "GameId", "PlatformSource", "PlatformSourceId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameListItems",
                table: "GameListItems",
                columns: new[] { "GameListId", "GameSource", "GameSourceId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameGenres",
                table: "GameGenres",
                columns: new[] { "GameId", "GenreSource", "GenreSourceId" });

            migrationBuilder.CreateTable(
                name: "GameCompanies",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CompanySource = table.Column<int>(type: "int", nullable: false),
                    CompanySourceId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameCompanies", x => new { x.GameId, x.CompanySource, x.CompanySourceId, x.Role });
                    table.ForeignKey(
                        name: "FK_GameCompanies_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IgdbCompanyOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IgdbCompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgdbCompanyOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IgdbGameOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IgdbGameId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Rating = table.Column<double>(type: "float", nullable: true),
                    ReplaceGenres = table.Column<bool>(type: "bit", nullable: false),
                    ReplacePlatforms = table.Column<bool>(type: "bit", nullable: false),
                    ReplaceCompanies = table.Column<bool>(type: "bit", nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgdbGameOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IgdbGenreOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IgdbGenreId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgdbGenreOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IgdbPlatformOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IgdbPlatformId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgdbPlatformOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IgdbGameCompanyOverrides",
                columns: table => new
                {
                    IgdbGameOverrideId = table.Column<int>(type: "int", nullable: false),
                    CompanySource = table.Column<int>(type: "int", nullable: false),
                    CompanySourceId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgdbGameCompanyOverrides", x => new { x.IgdbGameOverrideId, x.CompanySource, x.CompanySourceId, x.Role });
                    table.ForeignKey(
                        name: "FK_IgdbGameCompanyOverrides_IgdbGameOverrides_IgdbGameOverrideId",
                        column: x => x.IgdbGameOverrideId,
                        principalTable: "IgdbGameOverrides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IgdbGameGenreOverrides",
                columns: table => new
                {
                    IgdbGameOverrideId = table.Column<int>(type: "int", nullable: false),
                    GenreSource = table.Column<int>(type: "int", nullable: false),
                    GenreSourceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgdbGameGenreOverrides", x => new { x.IgdbGameOverrideId, x.GenreSource, x.GenreSourceId });
                    table.ForeignKey(
                        name: "FK_IgdbGameGenreOverrides_IgdbGameOverrides_IgdbGameOverrideId",
                        column: x => x.IgdbGameOverrideId,
                        principalTable: "IgdbGameOverrides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IgdbGamePlatformOverrides",
                columns: table => new
                {
                    IgdbGameOverrideId = table.Column<int>(type: "int", nullable: false),
                    PlatformSource = table.Column<int>(type: "int", nullable: false),
                    PlatformSourceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgdbGamePlatformOverrides", x => new { x.IgdbGameOverrideId, x.PlatformSource, x.PlatformSourceId });
                    table.ForeignKey(
                        name: "FK_IgdbGamePlatformOverrides_IgdbGameOverrides_IgdbGameOverrideId",
                        column: x => x.IgdbGameOverrideId,
                        principalTable: "IgdbGameOverrides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_Name",
                table: "Platforms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Name",
                table: "Genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GamePlatforms_PlatformSource_PlatformSourceId",
                table: "GamePlatforms",
                columns: new[] { "PlatformSource", "PlatformSourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameLists_UserId_Name",
                table: "GameLists",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameGenres_GenreSource_GenreSourceId",
                table: "GameGenres",
                columns: new[] { "GenreSource", "GenreSourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                table: "Companies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameCompanies_CompanySource_CompanySourceId",
                table: "GameCompanies",
                columns: new[] { "CompanySource", "CompanySourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_IgdbCompanyOverrides_IgdbCompanyId",
                table: "IgdbCompanyOverrides",
                column: "IgdbCompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IgdbGameCompanyOverrides_CompanySource_CompanySourceId",
                table: "IgdbGameCompanyOverrides",
                columns: new[] { "CompanySource", "CompanySourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_IgdbGameGenreOverrides_GenreSource_GenreSourceId",
                table: "IgdbGameGenreOverrides",
                columns: new[] { "GenreSource", "GenreSourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_IgdbGameOverrides_IgdbGameId",
                table: "IgdbGameOverrides",
                column: "IgdbGameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IgdbGamePlatformOverrides_PlatformSource_PlatformSourceId",
                table: "IgdbGamePlatformOverrides",
                columns: new[] { "PlatformSource", "PlatformSourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_IgdbGenreOverrides_IgdbGenreId",
                table: "IgdbGenreOverrides",
                column: "IgdbGenreId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IgdbPlatformOverrides_IgdbPlatformId",
                table: "IgdbPlatformOverrides",
                column: "IgdbPlatformId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameCompanies");

            migrationBuilder.DropTable(
                name: "IgdbCompanyOverrides");

            migrationBuilder.DropTable(
                name: "IgdbGameCompanyOverrides");

            migrationBuilder.DropTable(
                name: "IgdbGameGenreOverrides");

            migrationBuilder.DropTable(
                name: "IgdbGamePlatformOverrides");

            migrationBuilder.DropTable(
                name: "IgdbGenreOverrides");

            migrationBuilder.DropTable(
                name: "IgdbPlatformOverrides");

            migrationBuilder.DropTable(
                name: "IgdbGameOverrides");

            migrationBuilder.DropIndex(
                name: "IX_Platforms_Name",
                table: "Platforms");

            migrationBuilder.DropIndex(
                name: "IX_Genres_Name",
                table: "Genres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GamePlatforms",
                table: "GamePlatforms");

            migrationBuilder.DropIndex(
                name: "IX_GamePlatforms_PlatformSource_PlatformSourceId",
                table: "GamePlatforms");

            migrationBuilder.DropIndex(
                name: "IX_GameLists_UserId_Name",
                table: "GameLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameListItems",
                table: "GameListItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameGenres",
                table: "GameGenres");

            migrationBuilder.DropIndex(
                name: "IX_GameGenres_GenreSource_GenreSourceId",
                table: "GameGenres");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Name",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PlatformSourceId",
                table: "GamePlatforms");

            migrationBuilder.DropColumn(
                name: "GameSourceId",
                table: "GameListItems");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "GameListItems");

            migrationBuilder.DropColumn(
                name: "GameTitle",
                table: "GameListItems");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "GameListItems");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "GameListItems");

            migrationBuilder.DropColumn(
                name: "GenreSourceId",
                table: "GameGenres");

            migrationBuilder.RenameColumn(
                name: "PlatformSource",
                table: "GamePlatforms",
                newName: "PlatformId");

            migrationBuilder.RenameColumn(
                name: "GameSource",
                table: "GameListItems",
                newName: "GameId");

            migrationBuilder.RenameColumn(
                name: "GenreSource",
                table: "GameGenres",
                newName: "GenreId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Platforms",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Genres",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ReleaseDate",
                table: "Games",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Games",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Games",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CoverImageUrl",
                table: "Games",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeveloperCompanyId",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddPrimaryKey(
                name: "PK_GamePlatforms",
                table: "GamePlatforms",
                columns: new[] { "GameId", "PlatformId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameListItems",
                table: "GameListItems",
                columns: new[] { "GameListId", "GameId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameGenres",
                table: "GameGenres",
                columns: new[] { "GameId", "GenreId" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_DeveloperCompanyId",
                table: "Games",
                column: "DeveloperCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_GamePlatforms_PlatformId",
                table: "GamePlatforms",
                column: "PlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_GameLists_UserId_Name",
                table: "GameLists",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_GameListItems_GameId",
                table: "GameListItems",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameGenres_GenreId",
                table: "GameGenres",
                column: "GenreId");

            migrationBuilder.AddForeignKey(
                name: "FK_GameGenres_Genres_GenreId",
                table: "GameGenres",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameListItems_Games_GameId",
                table: "GameListItems",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GamePlatforms_Platforms_PlatformId",
                table: "GamePlatforms",
                column: "PlatformId",
                principalTable: "Platforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Companies_DeveloperCompanyId",
                table: "Games",
                column: "DeveloperCompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
