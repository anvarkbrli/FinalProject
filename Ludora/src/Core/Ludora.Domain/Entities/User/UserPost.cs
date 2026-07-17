namespace Ludora.Domain.Entities.User;

public class UserPost
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public string? DeleteReason { get; set; }

    public AppUser User { get; set; } = null!;
}
