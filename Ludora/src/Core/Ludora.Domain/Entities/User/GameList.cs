using Ludora.Domain.Entities.Base;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public class GameList : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public ListVisibility Visibility { get; set; } = ListVisibility.Public;
        public bool IsRanked { get; set; }
        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;
        public ICollection<GameListItem> GameListItems { get; set; } = new List<GameListItem>();
        public ICollection<GameListLike> Likes { get; set; } = new List<GameListLike>();
        public ICollection<GameListComment> Comments { get; set; } = new List<GameListComment>();
    }
}
