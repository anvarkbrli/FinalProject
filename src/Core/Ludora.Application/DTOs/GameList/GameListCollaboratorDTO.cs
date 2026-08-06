using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record GameListCollaboratorDTO
    {
        public Guid UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
        public GameListCollaborationStatus Status { get; init; }
        public DateTime InvitedAtUtc { get; init; }
        public DateTime? RespondedAtUtc { get; init; }
    }
}
