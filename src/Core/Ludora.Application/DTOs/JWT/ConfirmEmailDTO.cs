using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.JWT
{
    public record ConfirmEmailDTO
    {
        [Required(ErrorMessage = "İstifadəçi məlumatı tapılmadı.")]
        public Guid UserId { get; init; }

        [Required(ErrorMessage = "Email təsdiq tokeni tapılmadı. Linki yenidən açın.")]
        public string Token { get; init; } = string.Empty;
    }
}
