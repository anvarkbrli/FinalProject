using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class UserReportConfiguration : IEntityTypeConfiguration<UserReport>
{
    public void Configure(EntityTypeBuilder<UserReport> builder)
    {
        builder.ToTable("UserReports", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_UserReports_DifferentUsers",
                "[ReporterUserId] <> [ReportedUserId]");
            tableBuilder.HasCheckConstraint(
                "CK_UserReports_Category",
                "[Category] BETWEEN 1 AND 9");
            tableBuilder.HasCheckConstraint(
                "CK_UserReports_Status",
                "[Status] BETWEEN 0 AND 3");
            tableBuilder.HasCheckConstraint(
                "CK_UserReports_ContentType",
                "[ReportedContentType] BETWEEN 0 AND 8");
            tableBuilder.HasCheckConstraint(
                "CK_UserReports_ContentReference",
                "([ReportedContentType] = 0 AND [ReportedContentId] IS NULL) OR ([ReportedContentType] <> 0 AND [ReportedContentId] IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.EvidenceImageStorageKey)
            .HasMaxLength(2048);

        builder.Property(x => x.ReportedContentType)
            .IsRequired()
            .HasDefaultValue(UserReportContentType.UserProfile);

        builder.Property(x => x.ReportedContentSnapshot)
            .HasMaxLength(2000);

        builder.Property(x => x.ReportedContentUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasDefaultValue(UserReportStatus.Pending);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.AdminNote)
            .HasMaxLength(1000);

        builder.HasIndex(x => new
        {
            x.ReportedUserId,
            x.Status,
            x.CreatedAtUtc
        });

        builder.HasIndex(x => new
        {
            x.ReportedUserId,
            x.Category
        });

        builder.HasIndex(x => new
        {
            x.ReportedContentType,
            x.ReportedContentId
        });

        builder.HasIndex(x => new
        {
            x.ReporterUserId,
            x.CreatedAtUtc
        });

        builder.HasIndex(x => new
        {
            x.ReporterUserId,
            x.ReportedUserId,
            x.ReportedContentType,
            x.ReportedContentId
        })
        .IsUnique()
        .HasFilter("[Status] IN (0, 1)");

        builder.HasOne(x => x.ReporterUser)
            .WithMany(x => x.SubmittedReports)
            .HasForeignKey(x => x.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReportedUser)
            .WithMany(x => x.ReceivedReports)
            .HasForeignKey(x => x.ReportedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedByUser)
            .WithMany(x => x.ReviewedReports)
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
