using Ludora.Domain.Entities.Base;

namespace Ludora.Domain.Entities.Forum;

public sealed class ForumCommunityFlair : BaseEntity
{
    public int CommunityId { get; set; }
    public ForumCommunity Community { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string BackgroundColor { get; set; } = "#134E4A";
    public string TextColor { get; set; } = "#99F6E4";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ForumCommunityMember> Members { get; set; } = [];
}
