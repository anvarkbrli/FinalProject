namespace Ludora.Application.DTOs;

public sealed record DiscussionMediaDTO
{
    public int Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public int SortOrder { get; init; }
    public bool IsVideo => ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
}

public sealed record DiscussionMediaUploadDTO
{
    public Stream Stream { get; init; } = Stream.Null;
    public long Length { get; init; }
    public string ContentType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
}
