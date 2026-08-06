using Ludora.Domain.Entities.Forum;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class DiscussionPollOptionConfiguration : IEntityTypeConfiguration<DiscussionPollOption>
    {
        public void Configure(EntityTypeBuilder<DiscussionPollOption> builder)
        {
            builder.ToTable("DiscussionPollOption");
            builder.Property(x => x.Text).IsRequired().HasMaxLength(120);
            builder.HasAlternateKey(x => new { x.PollId, x.Id });
            builder.HasIndex(x => new { x.PollId, x.DisplayOrder }).IsUnique();
            builder.HasOne(x => x.Poll)
                .WithMany(x => x.Options)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
