using Ludora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public class GameListItemConfiguration : IEntityTypeConfiguration<GameListItem>
    {
        public void Configure(EntityTypeBuilder<GameListItem> builder)
        {
            builder.HasKey(x => new
            {
                x.GameListId,
                x.GameId
            });

            builder
                .HasOne(x => x.GameList)
                .WithMany(x => x.GameListItems)
                .HasForeignKey(x => x.GameListId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(x => x.Game)
                .WithMany(x => x.GameListItems)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.AddedAt).IsRequired();
        }
    }
}