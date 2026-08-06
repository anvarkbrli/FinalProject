using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class GameListCommentConfiguration : IEntityTypeConfiguration<GameListComment>
    {
        public void Configure(EntityTypeBuilder<GameListComment> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Content).IsRequired().HasMaxLength(2000);
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.HasIndex(x => new { x.GameListId, x.CreatedAt });
            builder.HasOne(x => x.GameList)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.GameListId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.User)
                .WithMany(x => x.GameListComments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
