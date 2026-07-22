using Ludora.Domain.Entities;
using Ludora.Domain.Entities.Forum;
using Ludora.Domain.Entities.GameTracking;
using Ludora.Domain.Entities.IGDB;
using Ludora.Domain.Entities.Steam;
using Ludora.Domain.Entities.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Game> Games {get; set;}
    public DbSet<Company> Companies {get; set;}
    public DbSet<Genre> Genres { get; set; }
    public DbSet<Platform> Platforms { get; set; }
    public DbSet<GameGenre> GameGenres { get; set; }
    public DbSet<GamePlatform> GamePlatforms { get; set; }
    public DbSet<GameCompany> GameCompanies { get; set; }
    public DbSet<GameRelation> GameRelations { get; set; }
    public DbSet<GameList> GameLists { get; set; }
    public DbSet<GameListItem> GameListItems { get; set; }
    public DbSet<UserFollow> UserFollows { get; set; }
    public DbSet<UserBlock> UserBlocks { get; set; }
    public DbSet<UserGame> UserGames { get; set; }
    public DbSet<UserPost> UserPosts { get; set; }
    public DbSet<PostLike> PostLikes { get; set; }
    public DbSet<PostComment> PostComments { get; set; }
    public DbSet<GameListLike> GameListLikes { get; set; }
    public DbSet<GameListComment> GameListComments { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ReviewLike> ReviewLikes { get; set; }
    public DbSet<ReviewComment> ReviewComments { get; set; }
    public DbSet<UserActivity> UserActivities { get; set; }
    public DbSet<ForumCategory> ForumCategories { get; set; }
    public DbSet<Discussion> Discussions { get; set; }
    public DbSet<DiscussionReply> DiscussionReplies { get; set; }
    public DbSet<DiscussionLike> DiscussionLikes { get; set; }
    public DbSet<DiscussionReplyLike> DiscussionReplyLikes { get; set; }
    public DbSet<UserBanHistory> UserBanHistories { get; set; }
    public DbSet<UserWarning> UserWarnings { get; set; }
    public DbSet<UserReport> UserReports { get; set; }
    public DbSet<ModeratorActionLog> ModeratorActionLogs { get; set; }
    public DbSet<Announcement> Announcements { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<NotificationPreference> NotificationPreferences { get; set; }
    public DbSet<NotificationOutboxMessage> NotificationOutboxMessages { get; set; }
    public DbSet<SteamLinkChallenge> SteamLinkChallenges { get; set; }
    public DbSet<SteamOwnedGame> SteamOwnedGames { get; set; }
    public DbSet<UserGamePlayLog> UserGamePlayLogs { get; set; }
    public DbSet<SteamFriendSnapshot> SteamFriendSnapshots { get; set; }

    public DbSet<IgdbGameOverride> IgdbGameOverrides { get; set; }
    public DbSet<IgdbGenreOverride> IgdbGenreOverrides { get; set; }
    public DbSet<IgdbPlatformOverride> IgdbPlatformOverrides { get; set; }
    public DbSet<IgdbCompanyOverride> IgdbCompanyOverrides { get; set; }
    public DbSet<IgdbGameGenreOverride> IgdbGameGenreOverrides { get; set; }
    public DbSet<IgdbGamePlatformOverride> IgdbGamePlatformOverrides { get; set; }
    public DbSet<IgdbGameCompanyOverride> IgdbGameCompanyOverrides { get; set; }
   
 

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<Game>(builder =>
        {
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Description).HasMaxLength(4000);
            builder.Property(x => x.CoverImageUrl).HasMaxLength(2048);
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Genre>(builder =>
        {
            builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(x => x.Name).IsUnique();
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Platform>(builder =>
        {
            builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(x => x.Name).IsUnique();
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Company>(builder =>
        {
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.HasIndex(x => x.Name).IsUnique();
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<GameGenre>(builder =>
        {
            builder.HasKey(x => new { x.GameId, x.GenreSource, x.GenreSourceId });
            builder.HasIndex(x => new { x.GenreSource, x.GenreSourceId });
            builder.HasOne(x => x.Game)
                .WithMany(x => x.GameGenres)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GamePlatform>(builder =>
        {
            builder.HasKey(x => new { x.GameId, x.PlatformSource, x.PlatformSourceId });
            builder.HasIndex(x => new { x.PlatformSource, x.PlatformSourceId });
            builder.HasOne(x => x.Game)
                .WithMany(x => x.GamePlatforms)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameCompany>(builder =>
        {
            builder.HasKey(x => new { x.GameId, x.CompanySource, x.CompanySourceId, x.Role });
            builder.HasIndex(x => new { x.CompanySource, x.CompanySourceId });
            builder.HasOne(x => x.Game)
                .WithMany(x => x.GameCompanies)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IgdbGameOverride>(builder =>
        {
            builder.HasIndex(x => x.IgdbGameId).IsUnique();
            builder.Property(x => x.Name).HasMaxLength(200);
            builder.Property(x => x.Description).HasMaxLength(4000);
            builder.Property(x => x.CoverImageUrl).HasMaxLength(2048);
        });

        modelBuilder.Entity<IgdbGenreOverride>(builder =>
        {
            builder.HasIndex(x => x.IgdbGenreId).IsUnique();
            builder.Property(x => x.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<IgdbPlatformOverride>(builder =>
        {
            builder.HasIndex(x => x.IgdbPlatformId).IsUnique();
            builder.Property(x => x.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<IgdbCompanyOverride>(builder =>
        {
            builder.HasIndex(x => x.IgdbCompanyId).IsUnique();
            builder.Property(x => x.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<IgdbGameGenreOverride>(builder =>
        {
            builder.HasKey(x => new { x.IgdbGameOverrideId, x.GenreSource, x.GenreSourceId });
            builder.HasIndex(x => new { x.GenreSource, x.GenreSourceId });
            builder.HasOne(x => x.GameOverride)
                .WithMany(x => x.Genres)
                .HasForeignKey(x => x.IgdbGameOverrideId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IgdbGamePlatformOverride>(builder =>
        {
            builder.HasKey(x => new { x.IgdbGameOverrideId, x.PlatformSource, x.PlatformSourceId });
            builder.HasIndex(x => new { x.PlatformSource, x.PlatformSourceId });
            builder.HasOne(x => x.GameOverride)
                .WithMany(x => x.Platforms)
                .HasForeignKey(x => x.IgdbGameOverrideId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IgdbGameCompanyOverride>(builder =>
        {
            builder.HasKey(x => new
            {
                x.IgdbGameOverrideId,
                x.CompanySource,
                x.CompanySourceId,
                x.Role
            });

            builder.HasIndex(x => new { x.CompanySource, x.CompanySourceId });

            builder.HasOne(x => x.GameOverride)
                .WithMany(x => x.Companies)
                .HasForeignKey(x => x.IgdbGameOverrideId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
