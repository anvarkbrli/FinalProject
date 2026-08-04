using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("EmailOutboxMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TemplateType).IsRequired();
        builder.Property(x => x.RecipientEmail).IsRequired().HasMaxLength(320);
        builder.Property(x => x.UserName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ActionUrl).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.EmailSubject).HasMaxLength(300);
        builder.Property(x => x.PlainTextBody);
        builder.Property(x => x.HtmlBody);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);

        builder.HasIndex(x => x.ContactMessageId);

        builder.HasIndex(x => new
        {
            x.ProcessedAtUtc,
            x.DeadLetteredAtUtc,
            x.NextAttemptAtUtc,
            x.LockedUntilUtc,
            x.CreatedAtUtc
        });
    }
}
