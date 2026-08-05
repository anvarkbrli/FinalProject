using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class DiscussionReplyLikeConfiguration : IEntityTypeConfiguration<DiscussionReplyLike>
{
    public void Configure(EntityTypeBuilder<DiscussionReplyLike> builder)
    {
        builder.HasKey(x => new { x.ReplyId, x.UserId });
        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.Reply)
            .WithMany(x => x.Likes)
            .HasForeignKey(x => x.ReplyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.DiscussionReplyLikes)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
