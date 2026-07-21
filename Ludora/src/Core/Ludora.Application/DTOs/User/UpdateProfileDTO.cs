using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs;

public record UpdateProfileDTO
{
    [Required]
    [StringLength(30, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "İstifadəçi adında yalnız hərf, rəqəm, nöqtə və alt xətt istifadə edilə bilər.")]
    public string UserName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Bio { get; set; }

    public IReadOnlyCollection<string> SocialLinks { get; set; } = [];
}
