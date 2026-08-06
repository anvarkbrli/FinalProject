namespace Ludora.Application.DTOs
{
    public sealed record InteractionCommentCreateDTO
    {
        public string Content { get; init; } = string.Empty;
    }
}
