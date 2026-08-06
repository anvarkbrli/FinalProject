using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class ChatMessageUserStateConfiguration
        : IEntityTypeConfiguration<ChatMessageUserState>
    {
        public void Configure(EntityTypeBuilder<ChatMessageUserState> builder)
        {
            builder.HasKey(x => new { x.MessageId, x.UserId });
            builder.Property(x => x.IsDeleted).IsRequired();

            builder.ToTable("ChatMessageUserStates", table =>
            {
                table.HasCheckConstraint(
                    "CK_ChatMessageUserStates_DeletedCannotBePinned",
                    "[IsDeleted] = 0 OR [PinnedAtUtc] IS NULL");
            });

            builder.HasIndex(x => new { x.UserId, x.PinnedAtUtc });
            builder.HasIndex(x => new { x.UserId, x.IsDeleted });
            builder.HasIndex(x => new { x.UserId, x.ReadAtUtc });
            builder.HasIndex(x => new { x.UserId, x.DeliveredAtUtc });

            builder.HasOne(x => x.Message)
                .WithMany(x => x.UserStates)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                .WithMany(x => x.ChatMessageStates)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
