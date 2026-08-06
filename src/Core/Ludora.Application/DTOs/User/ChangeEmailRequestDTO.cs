using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs
{
    public record ChangeEmailRequestDTO
    {
        [Required(ErrorMessage = "Yeni email ünvanını daxil edin.")]
        [EmailAddress(ErrorMessage = "Email ünvanının formatı düzgün deyil.")]
        [StringLength(128, ErrorMessage = "Email ünvanı maksimum 128 simvol ola bilər.")]
        public string NewEmail { get; init; } = string.Empty;

        [Required(ErrorMessage = "Cari şifrəni daxil edin.")]
        public string CurrentPassword { get; init; } = string.Empty;
    }
}
