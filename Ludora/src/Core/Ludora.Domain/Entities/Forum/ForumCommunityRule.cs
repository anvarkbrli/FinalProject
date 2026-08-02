using Ludora.Domain.Entities.Base;

namespace Ludora.Domain.Entities.Forum;

public sealed class ForumCommunityRule : BaseEntity
{
    public int CommunityId { get; set; }
    public ForumCommunity Community { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int SortOrder { get; set; }
}
