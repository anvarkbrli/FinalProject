using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public class UserPostConfiguration : IEntityTypeConfiguration<UserPost>
{
    public void Configure(EntityTypeBuilder<UserPost> builder)
    {
        builder.Property(x => x.Content)
            .HasMaxLength(2000);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.DeleteReason)
            .HasMaxLength(500);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Posts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt, x.Id })
            .IsDescending(false, true, true);
        builder.HasIndex(x => new { x.IsDeleted, x.CreatedAt, x.Id })
            .IsDescending(false, true, true);

        builder.ToTable(tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_UserPosts_ContentOrImage",
                "[Content] IS NOT NULL OR [ImageUrl] IS NOT NULL"));

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
