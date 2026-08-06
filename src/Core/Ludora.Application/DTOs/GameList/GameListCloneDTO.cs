using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record GameListCloneDTO
    {
        public string Name { get; init; } = string.Empty;
        public ListVisibility Visibility { get; init; } = ListVisibility.Private;
    }
}
