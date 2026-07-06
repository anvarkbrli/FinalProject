using Ludora.Domain.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Domain.Entities
{
    public class Game :BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateOnly ReleaseDate { get; set; }
        public string CoverImageUrl { get; set; }

        // Foreign Key
        public int DeveloperCompanyId { get; set; }
        public Company DeveloperCompany { get; set; }

        // Many-to-Many Cross Tables
        public ICollection<GameGenre> GameGenres { get; set; }
        public ICollection<GamePlatform> GamePlatforms { get; set; }

        // Stats (For fast filtering/sorting)
        public int PlayCount { get; set; } = 0;
        public double AverageRating { get; set; } = 0.0;
    }
}
