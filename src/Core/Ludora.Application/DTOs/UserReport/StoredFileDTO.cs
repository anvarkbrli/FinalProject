namespace Ludora.Application.DTOs;

public record StoredFileDTO
{
    public byte[] Content { get; init; } = [];
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = string.Empty;
}
