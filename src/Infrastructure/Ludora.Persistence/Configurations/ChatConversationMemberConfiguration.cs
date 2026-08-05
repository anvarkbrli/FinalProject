using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ChatConversationMemberConfiguration
    : IEntityTypeConfiguration<ChatConversationMember>
{
    public void Configure(EntityTypeBuilder<ChatConversationMember> builder)
    {
        builder.HasKey(x => new { x.ConversationId, x.UserId });
        builder.Property(x => x.JoinedAtUtc).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.JoinedAtUtc });

        builder.HasOne(x => x.Conversation)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.ChatConversationMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AddedByUser)
            .WithMany(x => x.AddedChatConversationMembers)
            .HasForeignKey(x => x.AddedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
