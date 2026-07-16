namespace Ludora.Application.DTOs;

public record DiscussionReplyUpdateDTO
{
    public string Content { get; init; } = null!;
}
