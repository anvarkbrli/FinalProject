namespace Ludora.Application.DTOs;

public record DiscussionCreateDTO
{
    public string Title { get; init; } = null!;
    public string Content { get; init; } = null!;
    public int CategoryId { get; init; }
}
