using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Ludora.API.Models;

public sealed class SingleImageFormRequest
{
    public IFormFile? Image { get; set; }
}

public sealed class PostCreateFormRequest
{
    public string? Content { get; set; }
    public IFormFile? Image { get; set; }
}

public sealed class PostUpdateFormRequest
{
    public string? Content { get; set; }
    public IFormFile? Image { get; set; }
    public bool RemoveImage { get; set; }
}

public sealed class UserReportCreateFormRequest
{
    public UserReportCategory Category { get; set; }
    public string? Description { get; set; }
    public UserReportContentType ReportedContentType { get; set; }
    public long? ReportedContentId { get; set; }
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<IFormFile> EvidenceImages { get; set; } = [];
    public IFormFile? EvidenceImage { get; set; }
}

public sealed class DiscussionCreateFormRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int CategoryId { get; set; }
    public int? CommunityId { get; set; }
    public bool IsSpoiler { get; set; }
    public string? PollJson { get; set; }
    public List<IFormFile> MediaFiles { get; set; } = [];
}

public sealed class DiscussionUpdateMediaFormRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int CategoryId { get; set; }
    public bool IsSpoiler { get; set; }
    public string? PollJson { get; set; }
    public List<int> KeptMediaIds { get; set; } = [];
    public List<IFormFile> MediaFiles { get; set; } = [];
}

public sealed class ForumCommunityCreateFormRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ForumCommunityCategory Category { get; set; }
    public ForumCommunityVisibility Visibility { get; set; }
    public bool HasIcon { get; set; }
    public bool HasCoverImage { get; set; }
    public List<IFormFile> ImageFiles { get; set; } = [];
}
