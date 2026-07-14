using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities;

public class GameGenre
{
    public int GameId { get; set; }

    public Game Game { get; set; } = null!;

    public CatalogSource GenreSource { get; set; }

    public long GenreSourceId { get; set; }
}