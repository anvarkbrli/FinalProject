using Ludora.Domain.Entities.Steam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class SteamPlaytimeDeltaConfiguration : IEntityTypeConfiguration<SteamPlaytimeDelta>
{
    public void Configure(EntityTypeBuilder<SteamPlaytimeDelta> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeltaMinutes).IsRequired();
        builder.Property(x => x.TotalPlaytimeMinutes).IsRequired();
        builder.Property(x => x.OccurredOn).IsRequired();
        builder.Property(x => x.SyncedAtUtc).IsRequired();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_SteamPlaytimeDeltas_DeltaMinutes",
                "[DeltaMinutes] > 0");
            table.HasCheckConstraint(
                "CK_SteamPlaytimeDeltas_TotalPlaytimeMinutes",
                "[TotalPlaytimeMinutes] >= [DeltaMinutes]");
        });

        builder.HasIndex(x => new { x.UserGamePlayLogId, x.SyncedAtUtc });
        builder.HasIndex(x => x.OccurredOn);

        builder.HasOne(x => x.UserGamePlayLog)
            .WithMany(x => x.Deltas)
            .HasForeignKey(x => x.UserGamePlayLogId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
