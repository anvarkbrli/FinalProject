namespace Ludora.Application.Models;

public sealed record IgdbPopularityPrimitiveModel
{
    public long GameId { get; init; }
    public long PopularityTypeId { get; init; }
    public decimal Value { get; init; }
}
