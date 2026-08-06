using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public class UserFollow
    {
        public Guid FollowerId { get; set; }

        public AppUser Follower { get; set; } = null!;

        public Guid FollowingId { get; set; }

        public AppUser Following { get; set; } = null!;

        public FollowStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcceptedAt { get; set; }
    }
}
