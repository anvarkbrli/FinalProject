using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.JWT;

public record LoginDTO
{
    [Required(ErrorMessage = "Email və ya istifadəçi adını daxil edin.")]
    public string EmailOrUserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifrəni daxil edin.")]
    public string Password { get; set; } = string.Empty;
}
