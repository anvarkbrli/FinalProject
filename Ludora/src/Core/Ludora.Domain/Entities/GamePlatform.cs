using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Domain.Entities
{
    public class GamePlatform
    {
        public int GameId { get; set; }
        public Game Game { get; set; }
        public int PlatformId { get; set; }
        public Platform Platform { get; set; }
    }
}
