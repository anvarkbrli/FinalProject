using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public sealed class PlatformDetailDTO
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public int ActiveGameCount { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? UpdatedAt { get; init; }
    }
}
