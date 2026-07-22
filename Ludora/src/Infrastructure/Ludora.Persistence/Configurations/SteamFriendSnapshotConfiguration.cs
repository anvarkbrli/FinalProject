using Ludora.Domain.Entities.Steam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class SteamFriendSnapshotConfiguration : IEntityTypeConfiguration<SteamFriendSnapshot>
{
    public void Configure(EntityTypeBuilder<SteamFriendSnapshot> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FriendSteamId).IsRequired().HasMaxLength(20);
        builder.Property(x => x.LastSeenAtUtc).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.FriendSteamId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsActive });

        builder.HasOne(x => x.User)
            .WithMany(x => x.SteamFriendSnapshots)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
