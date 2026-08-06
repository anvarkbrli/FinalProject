using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class UserBadgeConfiguration : IEntityTypeConfiguration<UserBadge>
    {
        public void Configure(EntityTypeBuilder<UserBadge> builder)
        {
            builder.HasKey(x => new { x.UserId, x.BadgeDefinitionId });
            builder.Property(x => x.MetricValueAtAward).IsRequired();
            builder.Property(x => x.AwardedAtUtc).IsRequired();
            builder.HasIndex(x => new { x.UserId, x.AwardedAtUtc })
                .IsDescending(false, true);

            builder.HasOne(x => x.User)
                .WithMany(x => x.Badges)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.BadgeDefinition)
                .WithMany(x => x.UserBadges)
                .HasForeignKey(x => x.BadgeDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
