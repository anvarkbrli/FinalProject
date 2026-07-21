using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs;

public record ChangePasswordDTO
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; init; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmNewPassword { get; init; } = string.Empty;
}
