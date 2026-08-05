using Ludora.Domain.Entities.Base;

namespace Ludora.Domain.Entities.Forum;

public class ForumCategory : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<Discussion> Discussions { get; set; } = [];
}
