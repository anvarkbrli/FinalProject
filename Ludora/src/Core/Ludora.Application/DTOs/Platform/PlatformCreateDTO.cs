using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record PlatformCreateDTO
    {
        public string Name { get; set; } = string.Empty;
    }

}
