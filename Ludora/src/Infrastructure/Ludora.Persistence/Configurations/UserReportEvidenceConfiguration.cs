using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class UserReportEvidenceConfiguration
    : IEntityTypeConfiguration<UserReportEvidence>
{
    public void Configure(EntityTypeBuilder<UserReportEvidence> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(260);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasIndex(x => new { x.UserReportId, x.DisplayOrder }).IsUnique();

        builder.HasOne(x => x.UserReport)
            .WithMany(x => x.EvidenceImages)
            .HasForeignKey(x => x.UserReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
