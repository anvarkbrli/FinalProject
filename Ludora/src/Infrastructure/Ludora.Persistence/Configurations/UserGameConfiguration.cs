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
            builder.Property(x => x.CreatedAt).IsRequired();

            builder.HasIndex(x => new
            {
                x.UserId,
                x.Source,
                x.GameId
            })
            .IsUnique();

            builder.HasOne(x => x.User)
                .WithMany(x => x.UserGames)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
