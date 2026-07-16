namespace Ludora.Application.DTOs;

public record DiscussionReplyCreateDTO
{
    public string Content { get; init; } = null!;
}
