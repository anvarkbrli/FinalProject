using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.JWT
{
    public record RegisterDTO
    {
        [Required(ErrorMessage = "İstifadəçi adını daxil edin.")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "İstifadəçi adı 3-30 simvol aralığında olmalıdır.")]
        [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "İstifadəçi adında yalnız hərf, rəqəm, nöqtə və alt xətt istifadə edilə bilər.")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email ünvanını daxil edin.")]
        [EmailAddress(ErrorMessage = "Email ünvanının formatı düzgün deyil.")]
        [StringLength(128, ErrorMessage = "Email ünvanı maksimum 128 simvol ola bilər.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifrəni daxil edin.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifrə 8-100 simvol aralığında olmalıdır.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifrəni təkrar daxil edin.")]
        [Compare(nameof(Password), ErrorMessage = "Şifrələr eyni deyil.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true", ErrorMessage = "İstifadə şərtləri və məxfilik siyasəti ilə razılaşmalısınız.")]
        public bool AcceptedTermsAndPrivacy { get; set; }
    }
}
