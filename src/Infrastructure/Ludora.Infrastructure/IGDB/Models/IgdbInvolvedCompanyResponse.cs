using System.Text.Json.Serialization;
namespace Ludora.Infrastructure.IGDB.Models
{
    internal sealed class IgdbInvolvedCompanyResponse
    {
        [JsonPropertyName("company")]
        public IgdbCompanyResponse? Company { get; set; }
        [JsonPropertyName("developer")]
        public bool Developer { get; set; }
        [JsonPropertyName("publisher")]
        public bool Publisher { get; set; }
    }
}
