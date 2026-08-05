using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.Forum;

public sealed class ForumCommunityEvent : BaseEntity
{
    public int CommunityId { get; set; }
    public ForumCommunity Community { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public ForumCommunityEventType Type { get; set; }
    public string? GameName { get; set; }
    public string? Platform { get; set; }
    public string? JoinUrl { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public int? MaxParticipants { get; set; }
    public bool IsCancelled { get; set; }

    public ICollection<ForumCommunityEventParticipant> Participants { get; set; } = [];
}
