namespace Ludora.Application.DTOs
{
    public record ReviewCommentCreateDTO
    {
        public string Content { get; init; } = string.Empty;
    }
}
