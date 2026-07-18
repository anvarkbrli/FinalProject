using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities
{
    public class UserGame
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public CatalogSource Source { get; set; }
        public long GameId { get; set; }
        public UserGameStatus? Status { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsWishlisted { get; set; }
        public double? Rating { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public AppUser User { get; set; } = null!;
    }
}
