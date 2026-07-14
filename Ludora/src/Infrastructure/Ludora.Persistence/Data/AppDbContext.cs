using Ludora.Domain.Entities;
using Ludora.Domain.Entities.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Game> Games { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<Platform> Platforms { get; set; }
    public DbSet<GameGenre> GameGenres { get; set; }
    public DbSet<GamePlatform> GamePlatforms { get; set; }
    public DbSet<GameList> GameLists { get; set; }
    public DbSet<GameListItem> GameListItems { get; set; }
    public DbSet<UserFollow> UserFollows { get; set; }
    public DbSet<UserBlock> UserBlocks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<GameGenre>()
            .HasKey(gg => new { gg.GameId, gg.GenreId });

        modelBuilder.Entity<GamePlatform>()
            .HasKey(gp => new { gp.GameId, gp.PlatformId });

        modelBuilder.Entity<Game>()
            .HasQueryFilter(g => !g.IsDeleted);

        modelBuilder.Entity<Company>()
            .HasQueryFilter(c => !c.IsDeleted);

        modelBuilder.Entity<Genre>()
            .HasQueryFilter(g => !g.IsDeleted);

        modelBuilder.Entity<Platform>()
            .HasQueryFilter(p => !p.IsDeleted);
    }
}