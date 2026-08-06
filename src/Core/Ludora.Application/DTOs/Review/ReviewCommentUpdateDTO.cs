namespace Ludora.Application.DTOs
{
    public record ReviewCommentUpdateDTO
    {
        public string Content { get; init; } = string.Empty;
    }
}
