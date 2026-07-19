namespace Ludora.Application.DTOs.Game;

public sealed record GameCommunityUserDTO
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
}
