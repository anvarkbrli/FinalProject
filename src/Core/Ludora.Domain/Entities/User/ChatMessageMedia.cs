namespace Ludora.Domain.Entities.User
{
    public sealed class ChatMessageMedia
    {
        public long Id { get; set; }
        public long MessageId { get; set; }
        public string StorageKey { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long Length { get; set; }
        public int SortOrder { get; set; }

        public ChatMessage Message { get; set; } = null!;
    }
}
