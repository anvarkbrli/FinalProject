using Ludora.Domain.Entities.Steam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class SteamOwnedGameConfiguration : IEntityTypeConfiguration<SteamOwnedGame>
{
    public void Configure(EntityTypeBuilder<SteamOwnedGame> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(300);
        builder.Property(x => x.IconUrl).HasMaxLength(1000);
        builder.Property(x => x.MatchedGameName).HasMaxLength(300);
        builder.Property(x => x.MatchedCoverImageUrl).HasMaxLength(2048);
        builder.Property(x => x.MatchConfidence).HasPrecision(5, 4);
        builder.Property(x => x.SyncedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.SteamAppId }).IsUnique();
        builder.HasIndex(x => x.LocalGameId);
        builder.HasIndex(x => new
        {
            x.UserId,
            x.PlaytimeMinutes,
            x.Name,
            x.SteamAppId
        })
            .IsDescending(false, true, false, false);

        builder.HasOne(x => x.User)
            .WithMany(x => x.SteamOwnedGames)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LocalGame)
            .WithMany(x => x.SteamOwnedGames)
            .HasForeignKey(x => x.LocalGameId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
