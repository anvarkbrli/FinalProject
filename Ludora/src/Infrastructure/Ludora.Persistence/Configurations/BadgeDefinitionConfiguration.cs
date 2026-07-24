using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class BadgeDefinitionConfiguration
    : IEntityTypeConfiguration<BadgeDefinition>
{
    private static readonly DateTime SeedDate =
        new(2026, 7, 23, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<BadgeDefinition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Icon).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Color).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Metric).IsRequired();
        builder.Property(x => x.Threshold).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.DisplayOrder });

        builder.ToTable("BadgeDefinitions", table =>
        {
            table.HasCheckConstraint(
                "CK_BadgeDefinitions_Threshold",
                "[Threshold] > 0");
        });

        builder.HasData(CreateSeedData());
    }

    private static BadgeDefinition[] CreateSeedData() =>
    [
        Badge(1, "first-blood", "İlk Qan", "İlk oyunu log et.", BadgeMetric.GamesLogged, 1, "Swords", "#ef4444"),
        Badge(2, "collector-10", "Kolleksiyaçı", "10 oyun log et.", BadgeMetric.GamesLogged, 10, "Library", "#8b5cf6"),
        Badge(3, "collector-50", "Böyük Kolleksiyaçı", "50 oyun log et.", BadgeMetric.GamesLogged, 50, "LibraryBig", "#7c3aed"),
        Badge(4, "collector-100", "Arxivçi", "100 oyun log et.", BadgeMetric.GamesLogged, 100, "Archive", "#6d28d9"),
        Badge(5, "played-1", "İlk Addım", "İlk oyunu tamamla.", BadgeMetric.GamesPlayed, 1, "Footprints", "#22c55e"),
        Badge(6, "played-25", "Oyunçu", "25 oyunu oynanmış kimi qeyd et.", BadgeMetric.GamesPlayed, 25, "Gamepad2", "#16a34a"),
        Badge(7, "played-100", "Veteran Oyunçu", "100 oyunu oynanmış kimi qeyd et.", BadgeMetric.GamesPlayed, 100, "Trophy", "#15803d"),
        Badge(8, "critic-1", "Tənqidçi", "İlk rəyini yaz.", BadgeMetric.ReviewsWritten, 1, "MessageSquareQuote", "#f59e0b"),
        Badge(9, "critic-10", "Söz Sahibi", "10 rəy yaz.", BadgeMetric.ReviewsWritten, 10, "MessagesSquare", "#d97706"),
        Badge(10, "critic-50", "Baş Tənqidçi", "50 rəy yaz.", BadgeMetric.ReviewsWritten, 50, "Medal", "#b45309"),
        Badge(11, "ratings-10", "Qiymətləndirici", "10 oyuna qiymət ver.", BadgeMetric.RatingsGiven, 10, "Star", "#eab308"),
        Badge(12, "ratings-100", "Ulduz Hakimi", "100 oyuna qiymət ver.", BadgeMetric.RatingsGiven, 100, "Sparkles", "#ca8a04"),
        Badge(13, "posts-1", "İlk Paylaşım", "İlk postunu paylaş.", BadgeMetric.PostsCreated, 1, "SquarePen", "#06b6d4"),
        Badge(14, "posts-25", "Kontent Yaradıcısı", "25 post paylaş.", BadgeMetric.PostsCreated, 25, "Clapperboard", "#0891b2"),
        Badge(15, "followers-10", "Populyar", "10 izləyici qazan.", BadgeMetric.Followers, 10, "Users", "#ec4899"),
        Badge(16, "followers-100", "İcma Ulduzu", "100 izləyici qazan.", BadgeMetric.Followers, 100, "Crown", "#db2777"),
        Badge(17, "followers-1000", "Əfsanə", "1000 izləyici qazan.", BadgeMetric.Followers, 1000, "Gem", "#be185d"),
        Badge(18, "following-10", "Sosial", "10 istifadəçini izlə.", BadgeMetric.Following, 10, "UserRoundPlus", "#14b8a6"),
        Badge(19, "following-100", "Şəbəkə Qurucusu", "100 istifadəçini izlə.", BadgeMetric.Following, 100, "Network", "#0d9488"),
        Badge(20, "lists-1", "Kurator", "İlk oyun siyahını yarat.", BadgeMetric.ListsCreated, 1, "ListPlus", "#3b82f6"),
        Badge(21, "lists-10", "Baş Kurator", "10 oyun siyahısı yarat.", BadgeMetric.ListsCreated, 10, "ListChecks", "#2563eb"),
        Badge(22, "favorites-10", "Sevimlilər", "10 oyunu sevimlilərə əlavə et.", BadgeMetric.Favorites, 10, "Heart", "#f43f5e"),
        Badge(23, "wishlist-25", "Arzu Siyahısı", "25 oyunu istək siyahısına əlavə et.", BadgeMetric.WishlistItems, 25, "Bookmark", "#a855f7"),
        Badge(24, "playtime-100", "Marafonçu", "100 saat oyun vaxtı topla.", BadgeMetric.TotalPlaytimeHours, 100, "Clock3", "#f97316"),
        Badge(25, "playtime-1000", "Zaman Ustası", "1000 saat oyun vaxtı topla.", BadgeMetric.TotalPlaytimeHours, 1000, "Hourglass", "#ea580c"),
        Badge(26, "forum-1", "Müzakirəçi", "İlk forum mövzusunu aç.", BadgeMetric.ForumDiscussions, 1, "MessagesSquare", "#0ea5e9"),
        Badge(27, "forum-replies-50", "İcma Səsi", "50 forum cavabı yaz.", BadgeMetric.ForumReplies, 50, "Megaphone", "#0284c7"),
        Badge(28, "steam-linked", "Steam Bağlantısı", "Steam hesabını Ludora-ya bağla.", BadgeMetric.SteamAccountLinked, 1, "Link2", "#64748b"),
        Badge(29, "steam-collector-50", "Steam Kolleksiyaçısı", "Steam-dən 50 oyun sinxronlaşdır.", BadgeMetric.SteamGamesImported, 50, "CloudDownload", "#475569"),
        Badge(30, "veteran-365", "Ludora Veteranı", "365 gün üzv ol.", BadgeMetric.AccountAgeDays, 365, "ShieldCheck", "#84cc16")
    ];

    private static BadgeDefinition Badge(
        int id,
        string code,
        string name,
        string description,
        BadgeMetric metric,
        int threshold,
        string icon,
        string color) =>
        new()
        {
            Id = id,
            Code = code,
            Name = name,
            Description = description,
            Metric = metric,
            Threshold = threshold,
            Icon = icon,
            Color = color,
            IsActive = true,
            DisplayOrder = id,
            CreatedAtUtc = SeedDate
        };
}
