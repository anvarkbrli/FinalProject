using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.Steam;
using Ludora.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Domain.Entities.GameTracking
{
    public class Game :BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateOnly? ReleaseDate { get; set; }
        public string? CoverImageUrl { get; set; }
        public int PlayCount { get; set; }
        public double AverageRating { get; set; }
        public string? Slug { get; set; } = null!;
        public int? SteamAppId { get; set; }
        public bool IsSteamFallback { get; set; }

        public GameType GameType { get; set; } = GameType.MainGame;
        public ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();
        public ICollection<GamePlatform> GamePlatforms { get; set; } = new List<GamePlatform>();
        public ICollection<GameCompany> GameCompanies { get; set; } = new List<GameCompany>();
        public ICollection<SteamOwnedGame> SteamOwnedGames { get; set; } = new List<SteamOwnedGame>();
    }
}
