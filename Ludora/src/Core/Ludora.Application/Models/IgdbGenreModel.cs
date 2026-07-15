namespace Ludora.Application.Models;

public sealed record IgdbGenreModel
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
