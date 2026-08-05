using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ForumCommunityFollowConfiguration : IEntityTypeConfiguration<ForumCommunityFollow>
{
    public void Configure(EntityTypeBuilder<ForumCommunityFollow> builder)
    {
        builder.HasKey(x => new { x.CommunityId, x.UserId });
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });

        builder.HasOne(x => x.Community)
            .WithMany(x => x.Followers)
            .HasForeignKey(x => x.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
