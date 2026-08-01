using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ChatConversationUserStateConfiguration
    : IEntityTypeConfiguration<ChatConversationUserState>
{
    public void Configure(EntityTypeBuilder<ChatConversationUserState> builder)
    {
        builder.HasKey(x => new { x.ConversationId, x.UserId });
        builder.Property(x => x.IsArchived).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.IsArchived, x.ArchivedAtUtc });

        builder.HasOne(x => x.Conversation)
            .WithMany(x => x.UserStates)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.ChatConversationStates)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
