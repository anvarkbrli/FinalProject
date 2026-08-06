using Ludora.Domain.Entities.User;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ludora.Persistence.Configurations
{
    public sealed class GameListCollaboratorConfiguration : IEntityTypeConfiguration<GameListCollaborator>
    {
        public void Configure(EntityTypeBuilder<GameListCollaborator> builder)
        {
            builder.HasKey(x => new { x.GameListId, x.UserId });

            builder.Property(x => x.Status).IsRequired();
            builder.Property(x => x.InvitedAtUtc).IsRequired();

            builder.HasOne(x => x.GameList)
                .WithMany(x => x.Collaborators)
                .HasForeignKey(x => x.GameListId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.InvitedByUser)
                .WithMany()
                .HasForeignKey(x => x.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.UserId, x.Status, x.InvitedAtUtc });
        }
    }
}
