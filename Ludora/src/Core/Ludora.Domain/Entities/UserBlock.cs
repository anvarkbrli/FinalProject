namespace Ludora.Domain.Entities;

public class UserBlock
{
    public Guid BlockerId { get; set; }

    public AppUser Blocker { get; set; } = null!;

    public Guid BlockedUserId { get; set; }

    public AppUser BlockedUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}