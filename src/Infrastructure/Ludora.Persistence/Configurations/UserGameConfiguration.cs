using Ludora.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public class UserGameConfiguration : IEntityTypeConfiguration<UserGame>
    {
        public void Configure(EntityTypeBuilder<UserGame> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserId).IsRequired();
            builder.Property(x => x.Source).IsRequired();
            builder.Property(x => x.GameId).IsRequired();
            builder.Property(x => x.IsFavorite).IsRequired().HasDefaultValue(false);
            builder.Property(x => x.IsWishlisted).IsRequired().HasDefaultValue(false);
            builder.Property(x => x.Rating);
            builder.Property(x => x.CreatedAt).IsRequired();

            builder.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_UserGames_Rating",
                    "[Rating] IS NULL OR ([Rating] >= 0.5 AND [Rating] <= 5 AND [Rating] * 2 = FLOOR([Rating] * 2))"));

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
                    x.GameId
                })
                .IncludeProperties(x => new
                {
                    x.Rating,
                    x.Status
                });

            builder.HasIndex(x => new
                {
                    x.UserId,
                    x.UpdatedAt,
                    x.CreatedAt,
                    x.Id
                })
                .IsDescending(false, true, true, true)
                .IncludeProperties(x => new
                {
                    x.Status,
                    x.IsFavorite,
                    x.IsWishlisted,
                    x.Rating
                });

            builder.HasOne(x => x.User)
                .WithMany(x => x.UserGames)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
