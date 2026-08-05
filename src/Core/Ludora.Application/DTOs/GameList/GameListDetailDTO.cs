using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record GameListDetailDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public ListVisibility Visibility { get; set; }

        public bool IsRanked { get; set; }

        public Guid OwnerId { get; set; }

        public string OwnerUserName { get; set; } = null!;

        public string? OwnerProfileImageUrl { get; set; }

        public bool IsOwner { get; set; }

        public bool CanEditGames { get; set; }

        public bool CanManageCollaborators { get; set; }

        public GameListCollaborationStatus? CurrentUserCollaborationStatus { get; set; }

        public int GameCount { get; set; }

        public int CurrentUserPlayedGameCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int LikeCount { get; set; }

        public int CommentCount { get; set; }

        public bool IsLikedByCurrentUser { get; set; }

        public string? BackgroundCoverImageUrl { get; set; }

        public List<GameListCollaboratorDTO> Collaborators { get; set; } = new();

        public List<GameListEntryDTO> Games { get; set; } = new();
    }
}