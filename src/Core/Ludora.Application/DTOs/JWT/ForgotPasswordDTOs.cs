using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.JWT
{
    public sealed record ForgotPasswordDTO
    {
        [Required(ErrorMessage = "Email ünvanını daxil edin.")]
        [EmailAddress(ErrorMessage = "Email ünvanının formatı düzgün deyil.")]
        [StringLength(128, ErrorMessage = "Email ünvanı maksimum 128 simvol ola bilər.")]
        public string Email { get; init; } = string.Empty;
    }

    public sealed record ResetPasswordDTO
    {
        [Required(ErrorMessage = "Email ünvanı tapılmadı.")]
        [EmailAddress(ErrorMessage = "Email ünvanının formatı düzgün deyil.")]
        [StringLength(128, ErrorMessage = "Email ünvanı maksimum 128 simvol ola bilər.")]
        public string Email { get; init; } = string.Empty;

        [Required(ErrorMessage = "Şifrə yeniləmə tokeni tapılmadı. Linki yenidən açın.")]
        public string Token { get; init; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifrəni daxil edin.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Yeni şifrə 8-100 simvol aralığında olmalıdır.")]
        public string NewPassword { get; init; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifrəni təkrar daxil edin.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Şifrələr eyni deyil.")]
        public string ConfirmNewPassword { get; init; } = string.Empty;
    }
}
