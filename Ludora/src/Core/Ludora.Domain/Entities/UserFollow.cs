using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Domain.Entities
{
    public class UserFollow
    {
        public Guid FollowerId { get; set; }

        public AppUser Follower { get; set; } = null!;

        public Guid FollowingId { get; set; }

        public AppUser Following { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
