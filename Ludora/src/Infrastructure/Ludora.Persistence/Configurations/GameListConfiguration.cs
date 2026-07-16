using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public class GameListConfiguration : IEntityTypeConfiguration<GameList>
    {
        public void Configure(EntityTypeBuilder<GameList> builder)
        {
            builder
                .Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder
                .Property(x => x.Description)
                .HasMaxLength(1000);

            builder
                .Property(x => x.Visibility)
                .IsRequired();

            builder
                .HasOne(x => x.User)
                .WithMany(x => x.GameLists)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasIndex(x => new
                {
                    x.UserId,
                    x.Name
                })
                .IsUnique();

            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}