using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public class UserPostConfiguration : IEntityTypeConfiguration<UserPost>
{
    public void Configure(EntityTypeBuilder<UserPost> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.DeleteReason)
            .HasMaxLength(500);

        builder.HasIndex(x => new
        {
            x.UserId,
            x.CreatedAt
        });

        builder.HasIndex(x => new
        {
            x.IsDeleted,
            x.CreatedAt
        });

        builder.HasOne(x => x.User)
            .WithMany(x => x.Posts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
