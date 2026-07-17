namespace Ludora.Application.DTOs;

public record UserWarningDTO
{
    public int Id { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public Guid WarnedByUserId { get; init; }
    public string WarnedByUserName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public bool IsRead { get; init; }
    public DateTime? ReadAtUtc { get; init; }
}
