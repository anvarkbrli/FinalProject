using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class DiscussionPollConfiguration : IEntityTypeConfiguration<DiscussionPoll>
{
    public void Configure(EntityTypeBuilder<DiscussionPoll> builder)
    {
        builder.ToTable("DiscussionPoll");
        builder.Property(x => x.Question).IsRequired().HasMaxLength(300);
        builder.Property(x => x.ResultsVisibility).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.DiscussionId).IsUnique();
        builder.HasOne(x => x.Discussion)
            .WithOne(x => x.Poll)
            .HasForeignKey<DiscussionPoll>(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
