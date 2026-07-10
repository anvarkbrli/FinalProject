using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Domain.Entities
{
    public class AppUser : IdentityUser<Guid>
    {
        public string? Bio { get; set; }

        public string? ProfileImageUrl { get; set; }

        public bool IsPrivate { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<GameList> GameLists { get; set; } = new List<GameList>();

        public AppUser()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            IsPrivate = false;
        }
    }
}
