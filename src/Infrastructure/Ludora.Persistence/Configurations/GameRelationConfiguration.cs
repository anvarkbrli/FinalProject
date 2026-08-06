using Ludora.Domain.Entities.GameTracking;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class GameRelationConfiguration : IEntityTypeConfiguration<GameRelation>
    {
        public void Configure(EntityTypeBuilder<GameRelation> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new
            {
                x.BaseGameSource,
                x.BaseGameId,
                x.RelatedGameSource,
                x.RelatedGameId,
                x.RelationType
            }).IsUnique();

            builder.HasIndex(x => new
            {
                x.BaseGameSource,
                x.BaseGameId
            });

            builder.HasIndex(x => new
            {
                x.RelatedGameSource,
                x.RelatedGameId
            });
        }
    }
}
