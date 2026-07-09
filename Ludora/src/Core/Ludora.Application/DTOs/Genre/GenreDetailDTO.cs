using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public sealed class GenreDetailDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int ActiveGameCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
