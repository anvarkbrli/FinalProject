using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record GameListReorderDTO
{
    public List<GameListReorderItemDTO> Items { get; init; } = [];
}

public record GameListReorderItemDTO
{
    public CatalogSource Source { get; init; }
    public long GameId { get; init; }
}
