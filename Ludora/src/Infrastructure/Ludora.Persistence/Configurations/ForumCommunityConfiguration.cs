using Ludora.Domain.Entities.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ForumCommunityConfiguration : IEntityTypeConfiguration<ForumCommunity>
{
    public void Configure(EntityTypeBuilder<ForumCommunity> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.IconUrl).HasMaxLength(2048);
        builder.Property(x => x.CoverImageUrl).HasMaxLength(2048);
        builder.Property(x => x.OwnershipTransferDeclinedUserIdsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.DeleteIfOwnershipTransferFails).HasDefaultValue(false);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.Category, x.CreatedAt });
        builder.HasIndex(x => new { x.Visibility, x.CreatedAt });
        builder.HasIndex(x => x.PendingOwnerCandidateId);

        builder.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
