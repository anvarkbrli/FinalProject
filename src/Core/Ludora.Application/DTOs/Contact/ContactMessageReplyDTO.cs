namespace Ludora.Application.DTOs.Contact;

public sealed record ContactMessageReplyDTO
{
    public string? Subject { get; init; }
    public string? Body { get; init; }
}
