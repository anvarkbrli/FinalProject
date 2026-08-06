namespace Ludora.Domain.Entities.User
{
    public sealed class PostComment
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }

        public UserPost Post { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
