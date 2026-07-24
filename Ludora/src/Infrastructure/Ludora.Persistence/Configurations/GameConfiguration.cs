using Ludora.Domain.Entities.GameTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public class GameConfiguration
        : IEntityTypeConfiguration<Game>
    {
        public void Configure(
            EntityTypeBuilder<Game> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Slug)
                .HasMaxLength(250);

            builder.HasIndex(x => x.Slug)
                .IsUnique();

            builder.HasIndex(x => x.SteamAppId)
                .IsUnique()
                .HasFilter("[SteamAppId] IS NOT NULL");

            builder.HasIndex(x => new { x.ReleaseDate, x.Name, x.Id })
                .IsDescending(true, false, false)
                .HasFilter("[IsDeleted] = 0");

            builder.HasIndex(x => new { x.Name, x.Id })
                .HasFilter("[IsDeleted] = 0");

            builder.HasIndex(x => new { x.AverageRating, x.Id })
                .HasFilter("[IsDeleted] = 0");

            builder.HasIndex(x => new { x.PlayCount, x.Id })
                .HasFilter("[IsDeleted] = 0");
        }
    }
}
