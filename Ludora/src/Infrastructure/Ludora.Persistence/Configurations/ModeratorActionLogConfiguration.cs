using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ModeratorActionLogConfiguration : IEntityTypeConfiguration<ModeratorActionLog>
{
    public void Configure(EntityTypeBuilder<ModeratorActionLog> builder)
    {
        builder.ToTable("ModeratorActionLogs", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_ModeratorActionLogs_ActionType",
                "[ActionType] BETWEEN 1 AND 5");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionType)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.TargetUserId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.UserReportId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.AdminUserId, x.CreatedAtUtc });

        builder.HasOne(x => x.AdminUser)
            .WithMany(x => x.PerformedModeratorActions)
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.TargetUser)
            .WithMany(x => x.ReceivedModeratorActions)
            .HasForeignKey(x => x.TargetUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.UserReport)
            .WithMany(x => x.ModeratorActions)
            .HasForeignKey(x => x.UserReportId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.UserWarning)
            .WithMany()
            .HasForeignKey(x => x.UserWarningId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.UserBanHistory)
            .WithMany()
            .HasForeignKey(x => x.UserBanHistoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
