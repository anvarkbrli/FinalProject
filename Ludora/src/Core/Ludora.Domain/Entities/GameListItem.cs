using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Domain.Entities
{
    public class GameListItem
    {
        public int GameListId { get; set; }

        public GameList GameList { get; set; } = null!;

        public int GameId { get; set; }

        public Game Game { get; set; } = null!;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
