using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Domain.Entities
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
