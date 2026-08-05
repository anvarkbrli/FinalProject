namespace Ludora.Domain.Entities.User;

public class UserWarning
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid WarnedByUserId { get; set; }
    public AppUser WarnedByUser { get; set; } = null!;

    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
