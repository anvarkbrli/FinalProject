namespace Ludora.Application.DTOs.JWT;

public record RegisterResultDTO
{
    public string Message { get; init; } = string.Empty;
    public bool EmailVerificationRequired { get; init; }
}
