using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MainForumSpoilerPollAndLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSpoiler",
                table: "Discussions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpoiler",
                table: "DiscussionMedia",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "AspNetUsers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "az");

            migrationBuilder.CreateTable(
                name: "DiscussionPoll",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiscussionId = table.Column<int>(type: "int", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResultsVisibility = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AllowVoteChange = table.Column<bool>(type: "bit", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionPoll", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionPoll_Discussions_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IgdbGameId = table.Column<long>(type: "bigint", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TranslatedText = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    SourceTextHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTranslations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionPollOption",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PollId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionPollOption", x => x.Id);
                    table.UniqueConstraint("AK_DiscussionPollOption_PollId_Id", x => new { x.PollId, x.Id });
                    table.ForeignKey(
                        name: "FK_DiscussionPollOption_DiscussionPoll_PollId",
                        column: x => x.PollId,
                        principalTable: "DiscussionPoll",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionPollVote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PollId = table.Column<int>(type: "int", nullable: false),
                    OptionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionPollVote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionPollVote_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscussionPollVote_DiscussionPollOption_PollId_OptionId",
                        columns: x => new { x.PollId, x.OptionId },
                        principalTable: "DiscussionPollOption",
                        principalColumns: new[] { "PollId", "Id" });
                    table.ForeignKey(
                        name: "FK_DiscussionPollVote_DiscussionPoll_PollId",
                        column: x => x.PollId,
                        principalTable: "DiscussionPoll",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionPoll_DiscussionId",
                table: "DiscussionPoll",
                column: "DiscussionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionPollOption_PollId_DisplayOrder",
                table: "DiscussionPollOption",
                columns: new[] { "PollId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionPollVote_PollId_OptionId",
                table: "DiscussionPollVote",
                columns: new[] { "PollId", "OptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionPollVote_PollId_UserId",
                table: "DiscussionPollVote",
                columns: new[] { "PollId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionPollVote_UserId",
                table: "DiscussionPollVote",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTranslations_IgdbGameId_LanguageCode_FieldName",
                table: "GameTranslations",
                columns: new[] { "IgdbGameId", "LanguageCode", "FieldName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscussionPollVote");

            migrationBuilder.DropTable(
                name: "GameTranslations");

            migrationBuilder.DropTable(
                name: "DiscussionPollOption");

            migrationBuilder.DropTable(
                name: "DiscussionPoll");

            migrationBuilder.DropColumn(
                name: "IsSpoiler",
                table: "Discussions");

            migrationBuilder.DropColumn(
                name: "IsSpoiler",
                table: "DiscussionMedia");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "AspNetUsers");

        }
    }
}
