namespace Ludora.Application.DTOs;

public record DiscussionUpdateDTO
{
    public string Title { get; init; } = null!;
    public string Content { get; init; } = null!;
    public int CategoryId { get; init; }
    public bool IsSpoiler { get; init; }
    public DiscussionPollWriteDTO? Poll { get; init; }

    // null olduqda mövcud media olduğu kimi saxlanılır.
    // Multipart redaktə sorğusunda saxlanılacaq media ID-ləri açıq göndərilir.
    public IReadOnlyList<int>? KeptMediaIds { get; init; }
    public IReadOnlyList<DiscussionMediaUploadDTO> Media { get; init; } = [];
}
