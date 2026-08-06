using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
    {
        public void Configure(EntityTypeBuilder<ContactMessage> builder)
        {
            builder.ToTable("ContactMessages");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
            builder.Property(x => x.Email).IsRequired().HasMaxLength(320);
            builder.Property(x => x.Subject).IsRequired().HasMaxLength(40);
            builder.Property(x => x.Message).IsRequired().HasMaxLength(5000).HasColumnType("nvarchar(max)");
            builder.Property(x => x.Language).IsRequired().HasMaxLength(5);
            builder.Property(x => x.Status).IsRequired();
            builder.Property(x => x.ReplySubject).HasMaxLength(300);
            builder.Property(x => x.ReplyBody).HasMaxLength(5000).HasColumnType("nvarchar(max)");

            builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
            builder.HasIndex(x => x.Email);
            builder.HasIndex(x => x.UserId);
        }
    }
}
