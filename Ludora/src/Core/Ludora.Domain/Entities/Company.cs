using Ludora.Domain.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Domain.Entities
{
    public class Company : BaseEntity
    {
        public string Name { get; set; }
        public ICollection<Game> DevelopedGames { get; set; }
    }
}
