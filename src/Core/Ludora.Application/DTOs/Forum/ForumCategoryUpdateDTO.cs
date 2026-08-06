namespace Ludora.Application.DTOs
{
    public record ForumCategoryUpdateDTO
    {
        public string Name { get; init; } = null!;
        public string? Description { get; init; }
    }
}
