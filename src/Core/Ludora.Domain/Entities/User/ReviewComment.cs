namespace Ludora.Domain.Entities.User
{
    public class ReviewComment
    {
        public long Id { get; set; }
        public long ReviewId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }

        public Review Review { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
