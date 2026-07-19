using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Source)
            .IsRequired();

        builder.Property(x => x.GameId)
            .IsRequired();

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(x => x.ContainsSpoilers)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.PlatformSource);

        builder.Property(x => x.PlatformId);

        builder.Property(x => x.PlaytimeMinutes);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
            {
                x.UserId,
                x.Source,
                x.GameId
            })
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.Source,
            x.GameId,
            x.CreatedAt
        });

        builder.HasIndex(x => new
        {
            x.UserId,
            x.CreatedAt
        });

        builder.HasIndex(x => new
        {
            x.PlatformSource,
            x.PlatformId
        });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Reviews_GameReference",
                "[GameId] > 0 AND [Source] IN (1, 2) AND ([Source] <> 1 OR [GameId] <= 2147483647)");

            table.HasCheckConstraint(
                "CK_Reviews_PlatformReference",
                "([PlatformSource] IS NULL AND [PlatformId] IS NULL) OR ([PlatformSource] IN (1, 2) AND [PlatformId] > 0 AND ([PlatformSource] <> 1 OR [PlatformId] <= 2147483647))");

            table.HasCheckConstraint(
                "CK_Reviews_PlaytimeMinutes",
                "[PlaytimeMinutes] IS NULL OR ([PlaytimeMinutes] >= 0 AND [PlaytimeMinutes] <= 600000)");
        });

        builder.HasOne(x => x.User)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
