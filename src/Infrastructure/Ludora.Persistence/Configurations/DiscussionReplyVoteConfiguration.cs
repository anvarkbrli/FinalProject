using Ludora.Domain.Entities.Forum;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class DiscussionReplyVoteConfiguration : IEntityTypeConfiguration<DiscussionReplyVote>
    {
        public void Configure(EntityTypeBuilder<DiscussionReplyVote> builder)
        {
            builder.HasKey(x => new { x.ReplyId, x.UserId });
            builder.HasIndex(x => x.UserId);

            builder.Property(x => x.Value)
                .HasColumnType("smallint")
                .IsRequired();

            builder.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_DiscussionReplyVotes_Value",
                    "[Value] IN (-1, 1)"));

            builder.HasOne(x => x.Reply)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => x.ReplyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
