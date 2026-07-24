namespace Ludora.Domain.Entities.User;

public sealed class UserBadge
{
    public Guid UserId { get; set; }
    public int BadgeDefinitionId { get; set; }
    public int MetricValueAtAward { get; set; }
    public DateTime AwardedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
    public BadgeDefinition BadgeDefinition { get; set; } = null!;
}
