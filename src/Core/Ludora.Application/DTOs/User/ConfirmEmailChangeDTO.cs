using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs
{
    public record ConfirmEmailChangeDTO
    {
        [Required(ErrorMessage = "İstifadəçi məlumatı tapılmadı.")]
        public Guid UserId { get; init; }

        [Required(ErrorMessage = "Yeni email ünvanı tapılmadı.")]
        [EmailAddress(ErrorMessage = "Email ünvanının formatı düzgün deyil.")]
        [StringLength(128, ErrorMessage = "Email ünvanı maksimum 128 simvol ola bilər.")]
        public string NewEmail { get; init; } = string.Empty;

        [Required(ErrorMessage = "Email dəyişiklik tokeni tapılmadı. Linki yenidən açın.")]
        public string Token { get; init; } = string.Empty;
    }
}
