using Ludora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
    {
        public void Configure(EntityTypeBuilder<AppUser> builder)
        {
            builder
                .Property(x => x.Bio)
                .HasMaxLength(500);

            builder
                .Property(x => x.ProfileImageUrl)
                .HasMaxLength(500);

            builder
                .Property(x => x.CreatedAt)
                .IsRequired();

            builder
                .Property(x => x.IsPrivate)
                .HasDefaultValue(false);

            builder
                .HasIndex(x => x.UserName)
                .IsUnique();

            builder
                .HasIndex(x => x.Email)
                .IsUnique();
        }
    }
}