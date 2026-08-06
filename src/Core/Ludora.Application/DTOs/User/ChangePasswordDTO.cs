using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs
{
    public record ChangePasswordDTO
    {
        public string CurrentPassword { get; init; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifrəni daxil edin.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Yeni şifrə 8-100 simvol aralığında olmalıdır.")]
        public string NewPassword { get; init; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifrəni təkrar daxil edin.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Şifrələr eyni deyil.")]
        public string ConfirmNewPassword { get; init; } = string.Empty;
    }
}
