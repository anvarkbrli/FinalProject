namespace Ludora.Application.DTOs
{
    public record ReviewCommentDTO
    {
        public long Id { get; init; }
        public long ReviewId { get; init; }
        public Guid UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
        public string Content { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public bool IsMine { get; init; }
    }
}
