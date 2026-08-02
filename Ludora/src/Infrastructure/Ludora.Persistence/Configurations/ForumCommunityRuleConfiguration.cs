using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ForumCommunityRuleConfiguration : IEntityTypeConfiguration<ForumCommunityRule>
{
    public void Configure(EntityTypeBuilder<ForumCommunityRule> builder)
    {
        builder.Property(x => x.Title).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.HasIndex(x => new { x.CommunityId, x.SortOrder });
        builder.HasOne(x => x.Community)
            .WithMany(x => x.Rules)
            .HasForeignKey(x => x.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
