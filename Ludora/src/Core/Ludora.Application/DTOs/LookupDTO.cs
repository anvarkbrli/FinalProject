using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record LookupDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
