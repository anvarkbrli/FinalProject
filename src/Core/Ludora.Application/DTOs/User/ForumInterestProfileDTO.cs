namespace Ludora.Application.DTOs;

public sealed record ForumInterestGameDTO
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

public sealed record ForumInterestProfileDTO
{
    public IReadOnlyList<string> Styles { get; init; } = [];
    public IReadOnlyList<ForumInterestGameDTO> Games { get; init; } = [];
    public IReadOnlyList<string> Genres { get; init; } = [];
    public string Platform { get; init; } = string.Empty;
    public DateTime CompletedAtUtc { get; init; }
}

public sealed record UpdateForumInterestProfileDTO
{
    public IReadOnlyList<string> Styles { get; init; } = [];
    public IReadOnlyList<ForumInterestGameDTO> Games { get; init; } = [];
    public IReadOnlyList<string> Genres { get; init; } = [];
    public string Platform { get; init; } = string.Empty;
}
