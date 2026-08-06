using Ludora.Domain.Entities.IGDB;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class GameTranslationConfiguration : IEntityTypeConfiguration<GameTranslation>
    {
        public void Configure(EntityTypeBuilder<GameTranslation> builder)
        {
            builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(10);
            builder.Property(x => x.FieldName).IsRequired().HasMaxLength(50);
            builder.Property(x => x.TranslatedText).IsRequired().HasMaxLength(10000);
            builder.Property(x => x.SourceTextHash).IsRequired().HasMaxLength(64);
            builder.HasIndex(x => new { x.IgdbGameId, x.LanguageCode, x.FieldName })
                .IsUnique();
        }
    }
}
