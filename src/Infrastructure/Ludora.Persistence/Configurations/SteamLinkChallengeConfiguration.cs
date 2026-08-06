using Ludora.Domain.Entities.Steam;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class SteamLinkChallengeConfiguration : IEntityTypeConfiguration<SteamLinkChallenge>
    {
        public void Configure(EntityTypeBuilder<SteamLinkChallenge> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.StateHash).IsRequired().HasMaxLength(64).IsFixedLength();
            builder.Property(x => x.CreatedAtUtc).IsRequired();
            builder.Property(x => x.ExpiresAtUtc).IsRequired();
            builder.HasIndex(x => x.StateHash).IsUnique();
            builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });

            builder.HasOne(x => x.User)
                .WithMany(x => x.SteamLinkChallenges)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
