namespace Ludora.Application.Models;

public sealed record IgdbPopularityTypeModel
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
