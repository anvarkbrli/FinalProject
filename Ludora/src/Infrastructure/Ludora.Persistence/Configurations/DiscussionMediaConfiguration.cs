using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class DiscussionMediaConfiguration : IEntityTypeConfiguration<DiscussionMedia>
{
    public void Configure(EntityTypeBuilder<DiscussionMedia> builder)
    {
        builder.Property(x => x.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(x => new { x.DiscussionId, x.SortOrder });

        builder.HasOne(x => x.Discussion)
            .WithMany(x => x.Media)
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
