using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ReviewLikeConfiguration : IEntityTypeConfiguration<ReviewLike>
{
    public void Configure(EntityTypeBuilder<ReviewLike> builder)
    {
        builder.HasKey(x => new { x.ReviewId, x.UserId });
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.ReviewId, x.CreatedAt });
        builder.HasOne(x => x.Review).WithMany(x => x.Likes).HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany(x => x.ReviewLikes).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
