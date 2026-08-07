using Ludora.Domain.Enums;
namespace Ludora.API.Controllers.User
{
    public sealed class ChatSendMessageFormRequest
    {
        public Guid? RecipientUserId { get; set; }
        public long? ConversationId { get; set; }
        public string? Text { get; set; }
        public string? GifUrl { get; set; }
        public CatalogSource? GameSource { get; set; }
        public long? GameId { get; set; }
        public string? GameName { get; set; }
        public string? GameCoverUrl { get; set; }
        public long? ReplyToMessageId { get; set; }
        public List<IFormFile> Media { get; set; } = [];
    }
}
