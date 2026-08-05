using Ludora.Domain.Entities.Base;

namespace Ludora.Domain.Entities.Forum;

public sealed class DiscussionMedia : BaseEntity
{
    public int DiscussionId { get; set; }
    public Discussion Discussion { get; set; } = null!;

    public string Url { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public long FileSize { get; set; }
    public int SortOrder { get; set; }
    public bool IsSpoiler { get; set; }
}
