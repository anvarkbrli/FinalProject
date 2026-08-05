namespace Ludora.Application.DTOs;

public record DiscussionCreateDTO
{
    public string Title { get; init; } = null!;
    public string? Content { get; init; }
    public int CategoryId { get; init; }
    public int? CommunityId { get; init; }
    public bool IsSpoiler { get; init; }
    public DiscussionPollWriteDTO? Poll { get; init; }
    public IReadOnlyList<DiscussionMediaUploadDTO> Media { get; init; } = [];
}
