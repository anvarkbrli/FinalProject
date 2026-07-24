using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class UserActivityConfiguration : IEntityTypeConfiguration<UserActivity>
{
    public void Configure(EntityTypeBuilder<UserActivity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.SubjectTitle)
            .HasMaxLength(300);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.UserId,
            x.CreatedAt,
            x.Id
        })
            .IsDescending(false, true, true);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
