using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record GameFilterDTO
    {
        public string? SearchName { get; set; }
        public int? CompanyId { get; set; }
        public int? GenreId { get; set; }
        public int? PlatformId { get; set; }
        public DateOnly? ReleaseYear { get; set; }

        // Sorting: "name_asc", "name_desc", "rating_desc", "play_desc", "year_desc"
        public string? SortBy { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
