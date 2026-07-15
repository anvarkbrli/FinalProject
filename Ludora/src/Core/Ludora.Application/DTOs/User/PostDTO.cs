namespace Ludora.Application.DTOs
{
    public record PostDTO
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsMine { get; set; }
    }

}

