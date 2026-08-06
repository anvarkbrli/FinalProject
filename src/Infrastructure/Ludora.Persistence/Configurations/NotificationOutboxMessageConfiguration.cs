using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class NotificationOutboxMessageConfiguration : IEntityTypeConfiguration<NotificationOutboxMessage>
    {
        public void Configure(EntityTypeBuilder<NotificationOutboxMessage> builder)
        {
            builder.ToTable("NotificationOutboxMessages");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.EventName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.LastError)
                .HasMaxLength(2000);

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.HasIndex(x => x.NotificationId)
                .IsUnique();

            builder.HasIndex(x => new
            {
                x.ProcessedAtUtc,
                x.DeadLetteredAtUtc,
                x.NextAttemptAtUtc,
                x.LockedUntilUtc,
                x.CreatedAtUtc
            });

            builder.HasOne(x => x.Notification)
                .WithOne(x => x.OutboxMessage)
                .HasForeignKey<NotificationOutboxMessage>(x => x.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
