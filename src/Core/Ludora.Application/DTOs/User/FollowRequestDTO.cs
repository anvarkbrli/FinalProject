namespace Ludora.Application.DTOs;

public record FollowRequestDTO
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? ProfileImageUrl { get; set; }

    public DateTime RequestedAt { get; set; }
}

public sealed record FollowRequestBulkActionDTO
{
    public List<Guid> RequesterIds { get; init; } = new();
}

public sealed record FollowRequestBulkResultDTO
{
    public int ProcessedCount { get; init; }
}
