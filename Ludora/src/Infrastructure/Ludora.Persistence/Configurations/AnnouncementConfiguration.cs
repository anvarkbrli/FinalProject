using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.IsPublished)
            .HasDefaultValue(false);

        builder.HasIndex(x => new
        {
            x.IsPublished,
            x.CreatedAt
        });

        builder.HasIndex(x => new
        {
            x.CreatedByUserId,
            x.CreatedAt
        });

        builder.HasIndex(x => new
        {
            x.IsDeleted,
            x.CreatedAt
        });

        builder.HasOne(x => x.CreatedByUser)
            .WithMany(x => x.Announcements)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
