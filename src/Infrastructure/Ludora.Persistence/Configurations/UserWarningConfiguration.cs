using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class UserWarningConfiguration : IEntityTypeConfiguration<UserWarning>
    {
        public void Configure(EntityTypeBuilder<UserWarning> builder)
        {
            builder.ToTable("UserWarnings");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Message)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.IsRead)
                .HasDefaultValue(false);

            builder.HasIndex(x => new
            {
                x.UserId,
                x.IsRead,
                x.CreatedAtUtc
            });

            builder.HasIndex(x => new
            {
                x.WarnedByUserId,
                x.CreatedAtUtc
            });

            builder.HasOne(x => x.User)
                .WithMany(x => x.ReceivedWarnings)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.WarnedByUser)
                .WithMany(x => x.SentWarnings)
                .HasForeignKey(x => x.WarnedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
