using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record GameCreateDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateOnly ReleaseDate { get; set; }
        public string CoverImageUrl { get; set; }
        public int DeveloperCompanyId { get; set; }
        public List<int> GenreIds { get; set; }
        public List<int> PlatformIds { get; set; }
    }
}
