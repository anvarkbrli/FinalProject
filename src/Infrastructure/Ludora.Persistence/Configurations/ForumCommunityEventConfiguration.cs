using Ludora.Domain.Entities.Forum;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class ForumCommunityEventConfiguration : IEntityTypeConfiguration<ForumCommunityEvent>
    {
        public void Configure(EntityTypeBuilder<ForumCommunityEvent> builder)
        {
            builder.Property(x => x.Title).IsRequired().HasMaxLength(120);
            builder.Property(x => x.Description).IsRequired().HasMaxLength(1500);
            builder.Property(x => x.GameName).HasMaxLength(120);
            builder.Property(x => x.Platform).HasMaxLength(80);
            builder.Property(x => x.JoinUrl).HasMaxLength(2048);
            builder.HasIndex(x => new { x.CommunityId, x.StartsAtUtc });
            builder.HasIndex(x => new { x.CreatedByUserId, x.StartsAtUtc });
            builder.HasOne(x => x.Community)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.CommunityId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
