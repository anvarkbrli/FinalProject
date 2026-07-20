using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record GameListUpdateDTO
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public ListVisibility? Visibility { get; set; }

        public bool? IsRanked { get; set; }
    }
}
