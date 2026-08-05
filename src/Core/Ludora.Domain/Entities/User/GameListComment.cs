namespace Ludora.Domain.Entities.User;

public sealed class GameListComment
{
    public long Id { get; set; }
    public int GameListId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public GameList GameList { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
