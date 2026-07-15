using Ludora.Domain.Entities;
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
        }
    }
}