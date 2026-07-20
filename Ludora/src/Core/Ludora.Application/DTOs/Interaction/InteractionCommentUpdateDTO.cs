namespace Ludora.Application.DTOs;

public sealed record InteractionCommentUpdateDTO
{
    public string Content { get; init; } = string.Empty;
}
