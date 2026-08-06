using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public class GameListCollaborator
    {
        public int GameListId { get; set; }
        public GameList GameList { get; set; } = null!;

        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;

        public Guid InvitedByUserId { get; set; }
        public AppUser InvitedByUser { get; set; } = null!;

        public GameListCollaborationStatus Status { get; set; } = GameListCollaborationStatus.Pending;
        public DateTime InvitedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAtUtc { get; set; }
    }
}
