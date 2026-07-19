using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs;

public record ChangeEmailRequestDTO
{
    [Required]
    [EmailAddress]
    [StringLength(128)]
    public string NewEmail { get; init; } = string.Empty;

    [Required]
    public string CurrentPassword { get; init; } = string.Empty;
}
