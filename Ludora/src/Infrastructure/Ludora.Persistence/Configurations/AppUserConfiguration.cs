using Ludora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Persistence.Configurations
{
    public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
    {
        public void Configure(
            EntityTypeBuilder<AppUser> builder
        )
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
        }
    }
}
