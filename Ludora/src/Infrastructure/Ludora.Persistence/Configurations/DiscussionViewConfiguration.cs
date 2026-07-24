using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class DiscussionViewConfiguration : IEntityTypeConfiguration<DiscussionView>
{
    public void Configure(EntityTypeBuilder<DiscussionView> builder)
    {
        builder.HasKey(x => new { x.DiscussionId, x.UserId });
        builder.HasIndex(x => new { x.UserId, x.FirstViewedAt });

        builder.HasOne(x => x.Discussion)
            .WithMany(x => x.Views)
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
