using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", table =>
        {
            table.HasCheckConstraint("CK_Notifications_Priority", "[Priority] BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_Notifications_EntityType", "[EntityType] BETWEEN 0 AND 9");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.EntityId)
            .HasMaxLength(64);

        builder.Property(x => x.ActionUrl)
            .HasMaxLength(500);

        builder.Property(x => x.DataJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.IsRead)
            .HasDefaultValue(false);

        builder.HasIndex(x => new
            {
                x.RecipientUserId,
                x.IsRead,
                x.Priority,
                x.CreatedAtUtc,
                x.Id
            })
            .IsDescending(false, false, true, true, true);

        builder.HasIndex(x => new
            {
                x.RecipientUserId,
                x.Type,
                x.CreatedAtUtc,
                x.Id
            })
            .IsDescending(false, false, true, true);

        builder.HasIndex(x => new { x.ActorUserId, x.CreatedAtUtc })
            .IsDescending(false, true);

        builder.HasOne(x => x.RecipientUser)
            .WithMany(x => x.ReceivedNotifications)
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ActorUser)
            .WithMany(x => x.SentNotifications)
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
