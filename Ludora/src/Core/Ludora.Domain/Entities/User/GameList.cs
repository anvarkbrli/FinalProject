using Ludora.Domain.Entities.Base;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public class GameList : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public ListVisibility Visibility { get; set; } = ListVisibility.Public;
        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;
        public ICollection<GameListItem> GameListItems { get; set; } = new List<GameListItem>();
    }
}
