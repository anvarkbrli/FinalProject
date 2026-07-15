using Microsoft.AspNetCore.Identity;

namespace Ludora.Domain.Entities.User
{
    public class AppUser : IdentityUser<Guid>
    {
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        public bool IsPrivate { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<GameList> GameLists { get; set; } = new List<GameList>();
        public ICollection<UserFollow> Followers { get; set; } = new List<UserFollow>();
        public ICollection<UserFollow> Following { get; set; } = new List<UserFollow>();
        public ICollection<UserBlock> BlockedUsers { get; set; } = new List<UserBlock>();
        public ICollection<UserBlock> BlockedByUsers { get; set; } = new List<UserBlock>();
        public ICollection<UserGame> UserGames { get; set; } = new HashSet<UserGame>();
        public ICollection<UserPost> Posts { get; set; } = new List<UserPost>();

    }
}
