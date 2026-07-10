using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record GameListCreateDTO
    {
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public ListVisibility Visibility { get; set; } = ListVisibility.Public;
    }
}
