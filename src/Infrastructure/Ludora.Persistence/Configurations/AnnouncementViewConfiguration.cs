using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class AnnouncementViewConfiguration : IEntityTypeConfiguration<AnnouncementView>
{
    public void Configure(EntityTypeBuilder<AnnouncementView> builder)
    {
        builder.ToTable("AnnouncementViews");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstViewedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.AnnouncementId, x.UserId })
            .IsUnique();

        builder.HasIndex(x => new { x.AnnouncementId, x.FirstViewedAtUtc });

        builder.HasOne(x => x.Announcement)
            .WithMany(x => x.Views)
            .HasForeignKey(x => x.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.AnnouncementViews)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
