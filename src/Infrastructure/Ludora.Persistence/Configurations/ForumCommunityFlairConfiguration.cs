using Ludora.Domain.Entities.Forum;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class ForumCommunityFlairConfiguration : IEntityTypeConfiguration<ForumCommunityFlair>
    {
        public void Configure(EntityTypeBuilder<ForumCommunityFlair> builder)
        {
            builder.Property(x => x.Name).IsRequired().HasMaxLength(40);
            builder.Property(x => x.BackgroundColor).IsRequired().HasMaxLength(7);
            builder.Property(x => x.TextColor).IsRequired().HasMaxLength(7);
            builder.HasIndex(x => new { x.CommunityId, x.SortOrder });
            builder.HasIndex(x => new { x.CommunityId, x.Name }).IsUnique();
            builder.HasOne(x => x.Community)
                .WithMany(x => x.Flairs)
                .HasForeignKey(x => x.CommunityId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
