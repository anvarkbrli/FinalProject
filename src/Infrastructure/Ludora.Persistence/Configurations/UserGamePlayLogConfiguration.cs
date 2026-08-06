using Ludora.Domain.Entities.Steam;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class UserGamePlayLogConfiguration : IEntityTypeConfiguration<UserGamePlayLog>
    {
        public void Configure(EntityTypeBuilder<UserGamePlayLog> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SteamAccountId)
                .IsRequired()
                .HasMaxLength(32);
            builder.Property(x => x.GameSource).IsRequired();
            builder.Property(x => x.GameId).IsRequired();
            builder.Property(x => x.InitialImportedPlaytimeMinutes).IsRequired();
            builder.Property(x => x.PlaytimeMinutes).IsRequired();
            builder.Property(x => x.FirstSyncedAtUtc).IsRequired();
            builder.Property(x => x.LastSyncedAtUtc).IsRequired();

            builder.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_UserGamePlayLogs_InitialImportedPlaytimeMinutes",
                    "[InitialImportedPlaytimeMinutes] >= 0");
                table.HasCheckConstraint(
                    "CK_UserGamePlayLogs_PlaytimeMinutes",
                    "[PlaytimeMinutes] >= 0");
            });

            builder.HasIndex(x => new
            {
                x.UserId,
                x.SteamAccountId,
                x.SteamAppId
            }).IsUnique();
            builder.HasIndex(x => new { x.UserId, x.GameSource, x.GameId });

            builder.HasOne(x => x.User)
                .WithMany(x => x.SteamPlayLogs)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
