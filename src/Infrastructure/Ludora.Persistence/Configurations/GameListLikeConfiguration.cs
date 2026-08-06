using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class GameListLikeConfiguration : IEntityTypeConfiguration<GameListLike>
    {
        public void Configure(EntityTypeBuilder<GameListLike> builder)
        {
            builder.HasKey(x => new { x.GameListId, x.UserId });
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.HasIndex(x => new { x.GameListId, x.CreatedAt });
            builder.HasOne(x => x.GameList)
                .WithMany(x => x.Likes)
                .HasForeignKey(x => x.GameListId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.User)
                .WithMany(x => x.GameListLikes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
