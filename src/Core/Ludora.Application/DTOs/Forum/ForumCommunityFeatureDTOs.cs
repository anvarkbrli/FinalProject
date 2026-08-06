using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record ForumCommunityRuleDTO
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int SortOrder { get; init; }
    }

    public record ForumCommunityRuleUpsertDTO
    {
        public int? Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    public record ForumCommunityRulesUpdateDTO
    {
        public IReadOnlyList<ForumCommunityRuleUpsertDTO> Rules { get; init; } = [];
    }

    public record ForumCommunityFlairDTO
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string BackgroundColor { get; init; } = string.Empty;
        public string TextColor { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public bool IsSelectedByCurrentUser { get; init; }
    }

    public record ForumCommunityFlairUpsertDTO
    {
        public int? Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string BackgroundColor { get; init; } = "#134E4A";
        public string TextColor { get; init; } = "#99F6E4";
    }

    public record ForumCommunityFlairsUpdateDTO
    {
        public IReadOnlyList<ForumCommunityFlairUpsertDTO> Flairs { get; init; } = [];
    }

    public record ForumCommunitySetFlairDTO
    {
        public int? FlairId { get; init; }
    }

    public record ForumCommunityMemberSearchDTO
    {
        public int MembershipId { get; init; }
        public Guid UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
        public ForumCommunityMemberRole Role { get; init; }
        public int? FlairId { get; init; }
        public string? FlairName { get; init; }
        public string? FlairBackgroundColor { get; init; }
        public string? FlairTextColor { get; init; }
        public DateTime JoinedAt { get; init; }
    }

    public record ForumCommunityEventDTO
    {
        public int Id { get; init; }
        public int CommunityId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public ForumCommunityEventType Type { get; init; }
        public string? GameName { get; init; }
        public string? Platform { get; init; }
        public string? JoinUrl { get; init; }
        public DateTime StartsAtUtc { get; init; }
        public DateTime? EndsAtUtc { get; init; }
        public int? MaxParticipants { get; init; }
        public int ParticipantCount { get; init; }
        public bool IsJoinedByCurrentUser { get; init; }
        public bool IsCancelled { get; init; }
        public Guid CreatedByUserId { get; init; }
        public string CreatedByUserName { get; init; } = string.Empty;
        public bool CanManage { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public record ForumCommunityEventUpsertDTO
    {
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public ForumCommunityEventType Type { get; init; }
        public string? GameName { get; init; }
        public string? Platform { get; init; }
        public string? JoinUrl { get; init; }
        public DateTime StartsAtUtc { get; init; }
        public DateTime? EndsAtUtc { get; init; }
        public int? MaxParticipants { get; init; }
    }
}
