using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record GameListItemDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string CoverImageUrl { get; set; }
        public string DeveloperCompanyName { get; set; }
        public int PlayCount { get; set; }
        public double AverageRating { get; set; }
        public DateOnly ReleaseDate { get; set; }
        public List<string> Genres { get; set; }
        public List<string> Platforms { get; set; }
    }
}
