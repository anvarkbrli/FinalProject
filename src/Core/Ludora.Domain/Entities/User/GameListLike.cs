namespace Ludora.Domain.Entities.User
{
    public sealed class GameListLike
    {
        public int GameListId { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }

        public GameList GameList { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
