namespace Ludora.Application.DTOs;

public sealed record SetUserGameRatingDTO
{
    public double? Rating { get; init; }
}
