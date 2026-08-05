namespace Ludora.Application.DTOs;

public record ForumCategoryCreateDTO
{
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
}
