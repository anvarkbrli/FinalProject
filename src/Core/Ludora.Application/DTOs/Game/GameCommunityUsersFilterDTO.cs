namespace Ludora.Application.DTOs.Game;

public sealed record GameCommunityUsersFilterDTO
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
