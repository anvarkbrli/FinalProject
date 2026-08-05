using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User;

public class UserActivity
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public UserActivityType Type { get; set; }
    public Guid? TargetUserId { get; set; }
    public CatalogSource? GameSource { get; set; }
    public long? GameId { get; set; }
    public long? ReviewId { get; set; }
    public UserGameStatus? Status { get; set; }
    public double? Rating { get; set; }
    public long? PostId { get; set; }
    public int? GameListId { get; set; }
    public string? SubjectTitle { get; set; }
    public DateTime CreatedAt { get; set; }

    public AppUser User { get; set; } = null!;
}
