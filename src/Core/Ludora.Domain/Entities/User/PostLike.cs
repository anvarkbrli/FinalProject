namespace Ludora.Domain.Entities.User;

public sealed class PostLike
{
    public long PostId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public UserPost Post { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
