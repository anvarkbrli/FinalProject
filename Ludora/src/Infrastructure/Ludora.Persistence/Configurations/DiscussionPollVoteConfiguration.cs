using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class DiscussionPollVoteConfiguration : IEntityTypeConfiguration<DiscussionPollVote>
{
    public void Configure(EntityTypeBuilder<DiscussionPollVote> builder)
    {
        builder.ToTable("DiscussionPollVote");
        builder.HasIndex(x => new { x.PollId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.PollId, x.OptionId });

        builder.HasOne(x => x.Poll)
            .WithMany(x => x.Votes)
            .HasForeignKey(x => x.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Option)
            .WithMany(x => x.Votes)
            .HasForeignKey(x => new { x.PollId, x.OptionId })
            .HasPrincipalKey(x => new { x.PollId, x.Id })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.User)
            .WithMany(x => x.DiscussionPollVotes)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
