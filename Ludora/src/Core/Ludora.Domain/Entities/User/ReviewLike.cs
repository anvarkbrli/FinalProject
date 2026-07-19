namespace Ludora.Domain.Entities.User;

public class ReviewLike
{
    public long ReviewId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Review Review { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
