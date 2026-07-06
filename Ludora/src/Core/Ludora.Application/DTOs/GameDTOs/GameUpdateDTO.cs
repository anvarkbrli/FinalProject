using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record GameUpdateDTO
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateOnly? ReleaseDate { get; set; }
        public string? CoverImageUrl { get; set; }
        public int? DeveloperCompanyId { get; set; }

        // Relation-lar null gələrsə, köhnə əlaqələrə toxunmuruq
        public List<int>? GenreIds { get; set; }
        public List<int>? PlatformIds { get; set; }
    }
}
