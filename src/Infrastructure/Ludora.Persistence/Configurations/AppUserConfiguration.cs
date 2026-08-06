using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
    {
        public void Configure(EntityTypeBuilder<AppUser> builder)
        {
            builder.Property(x => x.Bio)
                .HasMaxLength(500);

            builder.Property(x => x.ProfileImageUrl)
                .HasMaxLength(500);

            builder.Property(x => x.SocialLinksJson)
                .HasColumnType("nvarchar(max)");

            builder.Property(x => x.ForumInterestsJson)
                .HasColumnType("nvarchar(max)");

            builder.Property(x => x.TermsVersion)
                .HasMaxLength(32);

            builder.Property(x => x.PrivacyVersion)
                .HasMaxLength(32);

            builder.Property(x => x.SteamId)
                .HasMaxLength(20);

            builder.Property(x => x.SteamDisplayName)
                .HasMaxLength(200);

            builder.Property(x => x.SteamAvatarUrl)
                .HasMaxLength(1000);

            builder.Property(x => x.SteamProfileUrl)
                .HasMaxLength(1000);

            builder.Property(x => x.FpsGpuName)
                .HasMaxLength(150);

            builder.Property(x => x.FpsCpuName)
                .HasMaxLength(150);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => x.AccountDeletionRequestedAtUtc);

            builder.Property(x => x.IsPrivate)
                .HasDefaultValue(false);

            builder.Property(x => x.PreferredLanguage)
                .IsRequired()
                .HasMaxLength(10)
                .HasDefaultValue("az");

            builder.Property(x => x.PreferredTheme)
                .IsRequired()
                .HasMaxLength(10)
                .HasDefaultValue("dark");

            builder.Property(x => x.IsPermanentlyBanned)
                .HasDefaultValue(false);

            builder.Property(x => x.BanReason)
                .HasMaxLength(500);

            builder.HasIndex(x => x.UserName)
                .IsUnique();

            builder.HasIndex(x => x.Email)
                .IsUnique();

            builder.HasIndex(x => x.SteamId)
                .IsUnique()
                .HasFilter("[SteamId] IS NOT NULL");

            builder.HasIndex(x => new
            {
                x.IsPermanentlyBanned,
                x.BannedUntilUtc
            });
        }
    }
}
