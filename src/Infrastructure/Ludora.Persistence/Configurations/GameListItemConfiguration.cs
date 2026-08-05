using Ludora.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations;

public class GameListItemConfiguration : IEntityTypeConfiguration<GameListItem>
{
    public void Configure(EntityTypeBuilder<GameListItem> builder)
    {
        builder.HasKey(x => new
        {
            x.GameListId,
            x.GameSource,
            x.GameSourceId
        });

        builder
            .HasOne(x => x.GameList)
            .WithMany(x => x.GameListItems)
            .HasForeignKey(x => x.GameListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.GameTitle).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CoverImageUrl).HasMaxLength(2048);
        builder.Property(x => x.AddedAt).IsRequired();

        builder.HasIndex(x => new { x.GameListId, x.Position });

        builder.ToTable(tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_GameListItems_Position",
                "[Position] IS NULL OR [Position] >= 1"));
    }
}
