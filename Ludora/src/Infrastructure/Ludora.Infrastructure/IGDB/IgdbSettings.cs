using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Infrastructure.IGDB
{
    public sealed class IgdbSettings
    {
        public const string SectionName = "Igdb";
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = "https://api.igdb.com/v4/";
        public string TokenUrl { get; set; } = "https://id.twitch.tv/oauth2/token";
    }
}
