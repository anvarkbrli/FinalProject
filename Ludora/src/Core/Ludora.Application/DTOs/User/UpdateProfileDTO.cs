using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs;

public record UpdateProfileDTO
{
    [Required(ErrorMessage = "İstifadəçi adını daxil edin.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "İstifadəçi adı 3-30 simvol aralığında olmalıdır.")]
    [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "İstifadəçi adında yalnız hərf, rəqəm, nöqtə və alt xətt istifadə edilə bilər.")]
    public string UserName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Bio maksimum 500 simvol ola bilər.")]
    public string? Bio { get; set; }

    public IReadOnlyCollection<string> SocialLinks { get; set; } = [];
}
