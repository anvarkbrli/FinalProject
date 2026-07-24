using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class DiscussionReplyFollowConfiguration : IEntityTypeConfiguration<DiscussionReplyFollow>
{
    public void Configure(EntityTypeBuilder<DiscussionReplyFollow> builder)
    {
        builder.HasKey(x => new { x.ReplyId, x.UserId });
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });

        builder.HasOne(x => x.Reply)
            .WithMany(x => x.Followers)
            .HasForeignKey(x => x.ReplyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
