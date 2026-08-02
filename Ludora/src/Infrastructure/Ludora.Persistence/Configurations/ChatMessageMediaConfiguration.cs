using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ChatMessageMediaConfiguration : IEntityTypeConfiguration<ChatMessageMedia>
{
    public void Configure(EntityTypeBuilder<ChatMessageMedia> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StorageKey)
            .IsRequired()
            .HasMaxLength(512);
        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(x => x.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.Length).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();

        builder.HasIndex(x => new { x.MessageId, x.SortOrder })
            .IsUnique();
        builder.HasIndex(x => x.StorageKey);

        builder.HasOne(x => x.Message)
            .WithMany(x => x.MediaItems)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
