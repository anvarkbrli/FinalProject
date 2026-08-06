using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public sealed class ChatConversation
    {
        public long Id { get; set; }
        public Guid? UserOneId { get; set; }
        public Guid? UserTwoId { get; set; }
        public bool IsGroup { get; set; }
        public string? GroupName { get; set; }
        public string? GroupDescription { get; set; }
        public string? GroupImageUrl { get; set; }
        public Guid InitiatedByUserId { get; set; }
        public ChatRequestStatus RequestStatus { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? RespondedAtUtc { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateOnly? LastCompletedStreakDate { get; set; }

        public AppUser? UserOne { get; set; }
        public AppUser? UserTwo { get; set; }
        public AppUser InitiatedByUser { get; set; } = null!;
        public ICollection<ChatMessage> Messages { get; set; } = [];
        public ICollection<ChatConversationUserState> UserStates { get; set; } = [];
        public ICollection<ChatConversationMember> Members { get; set; } = [];
    }
}
