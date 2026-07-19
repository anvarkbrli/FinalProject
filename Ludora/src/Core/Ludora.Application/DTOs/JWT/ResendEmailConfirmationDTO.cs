using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.JWT;

public record ResendEmailConfirmationDTO
{
    [Required]
    [EmailAddress]
    [StringLength(128)]
    public string Email { get; init; } = string.Empty;
}
