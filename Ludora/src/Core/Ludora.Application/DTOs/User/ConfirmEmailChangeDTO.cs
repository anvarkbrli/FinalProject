using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs;

public record ConfirmEmailChangeDTO
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    [EmailAddress]
    [StringLength(128)]
    public string NewEmail { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;
}
