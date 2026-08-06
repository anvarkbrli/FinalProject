namespace Ludora.Application.DTOs
{
    public sealed record InteractionCommentFilterDTO
    {
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }
}
