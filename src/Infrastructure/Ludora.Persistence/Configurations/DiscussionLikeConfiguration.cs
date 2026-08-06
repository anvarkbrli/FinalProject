using Ludora.Domain.Entities.Forum;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class DiscussionLikeConfiguration : IEntityTypeConfiguration<DiscussionLike>
    {
        public void Configure(EntityTypeBuilder<DiscussionLike> builder)
        {
            builder.HasKey(x => new { x.DiscussionId, x.UserId });
            builder.HasIndex(x => x.UserId);

            builder.HasOne(x => x.Discussion)
                .WithMany(x => x.Likes)
                .HasForeignKey(x => x.DiscussionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                .WithMany(x => x.DiscussionLikes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
