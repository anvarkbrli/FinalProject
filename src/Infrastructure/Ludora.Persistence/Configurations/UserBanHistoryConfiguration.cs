using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class UserBanHistoryConfiguration : IEntityTypeConfiguration<UserBanHistory>
    {
        public void Configure(EntityTypeBuilder<UserBanHistory> builder)
        {
            builder.ToTable("UserBanHistories");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.BanType)
                .IsRequired();

            builder.Property(x => x.Reason)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(x => x.BannedAtUtc)
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.UserId,
                x.BannedAtUtc
            });

            builder.HasIndex(x => new
            {
                x.UserId,
                x.UnbannedAtUtc
            });

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.BannedByUser)
                .WithMany()
                .HasForeignKey(x => x.BannedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.UnbannedByUser)
                .WithMany()
                .HasForeignKey(x => x.UnbannedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
