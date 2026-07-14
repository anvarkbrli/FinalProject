using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public class UserFollowConfiguration
        : IEntityTypeConfiguration<UserFollow>
    {
        public void Configure(
            EntityTypeBuilder<UserFollow> builder
        )
        {
            builder.HasKey(x => new
            {
                x.FollowerId,
                x.FollowingId
            });

            builder
                .HasOne(x => x.Follower)
                .WithMany(x => x.Following)
                .HasForeignKey(x => x.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder
                .HasOne(x => x.Following)
                .WithMany(x => x.Followers)
                .HasForeignKey(x => x.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);

            builder
                .Property(x => x.CreatedAt)
                .IsRequired();
        }
    }
}