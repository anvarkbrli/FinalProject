using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.JWT
{
    public record ResendEmailConfirmationDTO
    {
        [Required(ErrorMessage = "Email ünvanını daxil edin.")]
        [EmailAddress(ErrorMessage = "Email ünvanının formatı düzgün deyil.")]
        [StringLength(128, ErrorMessage = "Email ünvanı maksimum 128 simvol ola bilər.")]
        public string Email { get; init; } = string.Empty;
    }
}
