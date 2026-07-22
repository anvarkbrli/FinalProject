using Ludora.Domain.Entities.Steam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class UserGamePlayLogConfiguration : IEntityTypeConfiguration<UserGamePlayLog>
{
    public void Configure(EntityTypeBuilder<UserGamePlayLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlaytimeMinutes).IsRequired();
        builder.Property(x => x.FirstSyncedAtUtc).IsRequired();
        builder.Property(x => x.LastSyncedAtUtc).IsRequired();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_UserGamePlayLogs_PlaytimeMinutes",
                "[PlaytimeMinutes] >= 0"));

        builder.HasIndex(x => new { x.UserId, x.SteamAppId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.GameId });

        builder.HasOne(x => x.User)
            .WithMany(x => x.SteamPlayLogs)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Game)
            .WithMany(x => x.SteamPlayLogs)
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
