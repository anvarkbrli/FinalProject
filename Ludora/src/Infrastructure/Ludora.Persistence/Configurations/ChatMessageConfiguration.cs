using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(4000);
        builder.Property(x => x.MediaStorageKey).HasMaxLength(2048);
        builder.Property(x => x.MediaContentType).HasMaxLength(100);
        builder.Property(x => x.GameName).HasMaxLength(300);
        builder.Property(x => x.GameCoverUrl).HasMaxLength(2048);
        builder.Property(x => x.SentAtUtc).IsRequired();
        builder.Property(x => x.DeletedForEveryoneAtUtc);

        builder.ToTable("ChatMessages", table =>
        {
            table.HasCheckConstraint(
                "CK_ChatMessages_Content",
                "[DeletedForEveryoneAtUtc] IS NOT NULL OR [Text] IS NOT NULL OR [MediaStorageKey] IS NOT NULL OR [GameId] IS NOT NULL");
            table.HasCheckConstraint(
                "CK_ChatMessages_Kind",
                "[Kind] BETWEEN 0 AND 3");
        });

        builder.HasIndex(x => new { x.ConversationId, x.SentAtUtc, x.Id })
            .IsDescending(false, true, true);
        builder.HasIndex(x => new
        {
            x.ConversationId,
            x.SenderUserId,
            x.ReadAtUtc,
            x.SentAtUtc
        });

        builder.HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SenderUser)
            .WithMany(x => x.SentChatMessages)
            .HasForeignKey(x => x.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
