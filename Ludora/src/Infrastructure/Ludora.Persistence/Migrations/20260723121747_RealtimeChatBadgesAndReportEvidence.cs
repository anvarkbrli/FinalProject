using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ludora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RealtimeChatBadgesAndReportEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserReports_Evidence",
                table: "UserReports");

            migrationBuilder.AlterColumn<decimal>(
                name: "MatchConfidence",
                table: "SteamOwnedGames",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.CreateTable(
                name: "BadgeDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Metric = table.Column<int>(type: "int", nullable: false),
                    Threshold = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeDefinitions", x => x.Id);
                    table.CheckConstraint("CK_BadgeDefinitions_Threshold", "[Threshold] > 0");
                });

            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserOneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserTwoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.Id);
                    table.CheckConstraint("CK_ChatConversations_DifferentUsers", "[UserOneId] <> [UserTwoId]");
                    table.CheckConstraint("CK_ChatConversations_RequestStatus", "[RequestStatus] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_ChatConversations_AspNetUsers_InitiatedByUserId",
                        column: x => x.InitiatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatConversations_AspNetUsers_UserOneId",
                        column: x => x.UserOneId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatConversations_AspNetUsers_UserTwoId",
                        column: x => x.UserTwoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserReportEvidenceImages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserReportId = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReportEvidenceImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReportEvidenceImages_UserReports_UserReportId",
                        column: x => x.UserReportId,
                        principalTable: "UserReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBadges",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BadgeDefinitionId = table.Column<int>(type: "int", nullable: false),
                    MetricValueAtAward = table.Column<int>(type: "int", nullable: false),
                    AwardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBadges", x => new { x.UserId, x.BadgeDefinitionId });
                    table.ForeignKey(
                        name: "FK_UserBadges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBadges_BadgeDefinitions_BadgeDefinitionId",
                        column: x => x.BadgeDefinitionId,
                        principalTable: "BadgeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MediaStorageKey = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MediaContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MediaLength = table.Column<long>(type: "bigint", nullable: true),
                    GameSource = table.Column<int>(type: "int", nullable: true),
                    GameId = table.Column<long>(type: "bigint", nullable: true),
                    GameName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    GameCoverUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.CheckConstraint("CK_ChatMessages_Content", "[Text] IS NOT NULL OR [MediaStorageKey] IS NOT NULL OR [GameId] IS NOT NULL");
                    table.CheckConstraint("CK_ChatMessages_Kind", "[Kind] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ChatMessages_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "BadgeDefinitions",
                columns: new[] { "Id", "Code", "Color", "CreatedAtUtc", "Description", "DisplayOrder", "Icon", "IsActive", "Metric", "Name", "Threshold", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "first-blood", "#ef4444", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "İlk oyunu log et.", 1, "Swords", true, 1, "İlk Qan", 1, null },
                    { 2, "collector-10", "#8b5cf6", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "10 oyun log et.", 2, "Library", true, 1, "Kolleksiyaçı", 10, null },
                    { 3, "collector-50", "#7c3aed", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "50 oyun log et.", 3, "LibraryBig", true, 1, "Böyük Kolleksiyaçı", 50, null },
                    { 4, "collector-100", "#6d28d9", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "100 oyun log et.", 4, "Archive", true, 1, "Arxivçi", 100, null },
                    { 5, "played-1", "#22c55e", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "İlk oyunu tamamla.", 5, "Footprints", true, 2, "İlk Addım", 1, null },
                    { 6, "played-25", "#16a34a", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "25 oyunu oynanmış kimi qeyd et.", 6, "Gamepad2", true, 2, "Oyunçu", 25, null },
                    { 7, "played-100", "#15803d", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "100 oyunu oynanmış kimi qeyd et.", 7, "Trophy", true, 2, "Veteran Oyunçu", 100, null },
                    { 8, "critic-1", "#f59e0b", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "İlk rəyini yaz.", 8, "MessageSquareQuote", true, 3, "Tənqidçi", 1, null },
                    { 9, "critic-10", "#d97706", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "10 rəy yaz.", 9, "MessagesSquare", true, 3, "Söz Sahibi", 10, null },
                    { 10, "critic-50", "#b45309", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "50 rəy yaz.", 10, "Medal", true, 3, "Baş Tənqidçi", 50, null },
                    { 11, "ratings-10", "#eab308", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "10 oyuna qiymət ver.", 11, "Star", true, 4, "Qiymətləndirici", 10, null },
                    { 12, "ratings-100", "#ca8a04", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "100 oyuna qiymət ver.", 12, "Sparkles", true, 4, "Ulduz Hakimi", 100, null },
                    { 13, "posts-1", "#06b6d4", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "İlk postunu paylaş.", 13, "SquarePen", true, 5, "İlk Paylaşım", 1, null },
                    { 14, "posts-25", "#0891b2", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "25 post paylaş.", 14, "Clapperboard", true, 5, "Kontent Yaradıcısı", 25, null },
                    { 15, "followers-10", "#ec4899", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "10 izləyici qazan.", 15, "Users", true, 6, "Populyar", 10, null },
                    { 16, "followers-100", "#db2777", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "100 izləyici qazan.", 16, "Crown", true, 6, "İcma Ulduzu", 100, null },
                    { 17, "followers-1000", "#be185d", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "1000 izləyici qazan.", 17, "Gem", true, 6, "Əfsanə", 1000, null },
                    { 18, "following-10", "#14b8a6", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "10 istifadəçini izlə.", 18, "UserRoundPlus", true, 7, "Sosial", 10, null },
                    { 19, "following-100", "#0d9488", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "100 istifadəçini izlə.", 19, "Network", true, 7, "Şəbəkə Qurucusu", 100, null },
                    { 20, "lists-1", "#3b82f6", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "İlk oyun siyahını yarat.", 20, "ListPlus", true, 8, "Kurator", 1, null },
                    { 21, "lists-10", "#2563eb", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "10 oyun siyahısı yarat.", 21, "ListChecks", true, 8, "Baş Kurator", 10, null },
                    { 22, "favorites-10", "#f43f5e", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "10 oyunu sevimlilərə əlavə et.", 22, "Heart", true, 9, "Sevimlilər", 10, null },
                    { 23, "wishlist-25", "#a855f7", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "25 oyunu istək siyahısına əlavə et.", 23, "Bookmark", true, 10, "Arzu Siyahısı", 25, null },
                    { 24, "playtime-100", "#f97316", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "100 saat oyun vaxtı topla.", 24, "Clock3", true, 11, "Marafonçu", 100, null },
                    { 25, "playtime-1000", "#ea580c", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "1000 saat oyun vaxtı topla.", 25, "Hourglass", true, 11, "Zaman Ustası", 1000, null },
                    { 26, "forum-1", "#0ea5e9", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "İlk forum mövzusunu aç.", 26, "MessagesSquare", true, 12, "Müzakirəçi", 1, null },
                    { 27, "forum-replies-50", "#0284c7", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "50 forum cavabı yaz.", 27, "Megaphone", true, 13, "İcma Səsi", 50, null },
                    { 28, "steam-linked", "#64748b", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Steam hesabını Ludora-ya bağla.", 28, "Link2", true, 17, "Steam Bağlantısı", 1, null },
                    { 29, "steam-collector-50", "#475569", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Steam-dən 50 oyun sinxronlaşdır.", 29, "CloudDownload", true, 16, "Steam Kolleksiyaçısı", 50, null },
                    { 30, "veteran-365", "#84cc16", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "365 gün üzv ol.", 30, "ShieldCheck", true, 0, "Ludora Veteranı", 365, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BadgeDefinitions_Code",
                table: "BadgeDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BadgeDefinitions_IsActive_DisplayOrder",
                table: "BadgeDefinitions",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_InitiatedByUserId",
                table: "ChatConversations",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserOneId_RequestStatus_UpdatedAtUtc",
                table: "ChatConversations",
                columns: new[] { "UserOneId", "RequestStatus", "UpdatedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserOneId_UserTwoId",
                table: "ChatConversations",
                columns: new[] { "UserOneId", "UserTwoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserTwoId_RequestStatus_UpdatedAtUtc",
                table: "ChatConversations",
                columns: new[] { "UserTwoId", "RequestStatus", "UpdatedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_SenderUserId_ReadAtUtc_SentAtUtc",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "SenderUserId", "ReadAtUtc", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_SentAtUtc_Id",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "SentAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderUserId",
                table: "ChatMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_BadgeDefinitionId",
                table: "UserBadges",
                column: "BadgeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_UserId_AwardedAtUtc",
                table: "UserBadges",
                columns: new[] { "UserId", "AwardedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserReportEvidenceImages_UserReportId_DisplayOrder",
                table: "UserReportEvidenceImages",
                columns: new[] { "UserReportId", "DisplayOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "UserBadges");

            migrationBuilder.DropTable(
                name: "UserReportEvidenceImages");

            migrationBuilder.DropTable(
                name: "ChatConversations");

            migrationBuilder.DropTable(
                name: "BadgeDefinitions");

            migrationBuilder.AlterColumn<decimal>(
                name: "MatchConfidence",
                table: "SteamOwnedGames",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)",
                oldPrecision: 5,
                oldScale: 4);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserReports_Evidence",
                table: "UserReports",
                sql: "[Description] IS NOT NULL OR [EvidenceImageStorageKey] IS NOT NULL OR [ReportedContentId] IS NOT NULL");

        }
    }
}
