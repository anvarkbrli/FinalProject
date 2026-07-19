using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.JWT;

public record ConfirmEmailDTO
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public string Token { get; init; } = string.Empty;
}
