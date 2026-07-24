using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ChatConversationConfiguration
    : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestStatus)
            .IsRequired()
            .HasDefaultValue(ChatRequestStatus.Pending);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.ToTable("ChatConversations", table =>
        {
            table.HasCheckConstraint(
                "CK_ChatConversations_DifferentUsers",
                "[UserOneId] <> [UserTwoId]");
            table.HasCheckConstraint(
                "CK_ChatConversations_RequestStatus",
                "[RequestStatus] BETWEEN 0 AND 2");
        });

        builder.HasIndex(x => new { x.UserOneId, x.UserTwoId }).IsUnique();
        builder.HasIndex(x => new { x.UserOneId, x.RequestStatus, x.UpdatedAtUtc })
            .IsDescending(false, false, true);
        builder.HasIndex(x => new { x.UserTwoId, x.RequestStatus, x.UpdatedAtUtc })
            .IsDescending(false, false, true);

        builder.HasOne(x => x.UserOne)
            .WithMany(x => x.ChatConversationsAsUserOne)
            .HasForeignKey(x => x.UserOneId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UserTwo)
            .WithMany(x => x.ChatConversationsAsUserTwo)
            .HasForeignKey(x => x.UserTwoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InitiatedByUser)
            .WithMany(x => x.InitiatedChatConversations)
            .HasForeignKey(x => x.InitiatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
