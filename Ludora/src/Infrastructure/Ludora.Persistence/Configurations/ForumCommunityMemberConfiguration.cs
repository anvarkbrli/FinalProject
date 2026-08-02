using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ForumCommunityMemberConfiguration : IEntityTypeConfiguration<ForumCommunityMember>
{
    public void Configure(EntityTypeBuilder<ForumCommunityMember> builder)
    {
        builder.HasIndex(x => new { x.CommunityId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });

        builder.HasOne(x => x.Community)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Flair)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.FlairId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
