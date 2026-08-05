using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ChatMessageReactionConfiguration
    : IEntityTypeConfiguration<ChatMessageReaction>
{
    public void Configure(EntityTypeBuilder<ChatMessageReaction> builder)
    {
        builder.HasKey(x => new { x.MessageId, x.UserId });
        builder.Property(x => x.ReactionType).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.ToTable("ChatMessageReactions", table =>
        {
            table.HasCheckConstraint(
                "CK_ChatMessageReactions_ReactionType",
                "[ReactionType] BETWEEN 0 AND 5");
        });

        builder.HasIndex(x => new { x.MessageId, x.ReactionType });
        builder.HasIndex(x => new { x.UserId, x.UpdatedAtUtc });

        builder.HasOne(x => x.Message)
            .WithMany(x => x.Reactions)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.ChatMessageReactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
