using Ludora.Domain.Entities.Forum;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class ForumCommunityEventParticipantConfiguration : IEntityTypeConfiguration<ForumCommunityEventParticipant>
    {
        public void Configure(EntityTypeBuilder<ForumCommunityEventParticipant> builder)
        {
            builder.HasKey(x => new { x.EventId, x.UserId });
            builder.HasIndex(x => new { x.UserId, x.JoinedAtUtc });
            builder.HasOne(x => x.Event)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
