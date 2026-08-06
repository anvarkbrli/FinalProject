using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
    {
        public void Configure(EntityTypeBuilder<NotificationPreference> builder)
        {
            builder.ToTable("NotificationPreferences", table =>
                table.HasCheckConstraint("CK_NotificationPreferences_Audience", "[Audience] BETWEEN 1 AND 3"));

            builder.HasKey(x => new { x.UserId, x.Type });

            builder.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            builder.HasIndex(x => new { x.UserId, x.Audience });

            builder.HasOne(x => x.User)
                .WithMany(x => x.NotificationPreferences)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
