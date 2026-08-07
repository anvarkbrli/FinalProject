namespace Ludora.Application.Extensions
{
    using Ludora.Application.Exceptions;
    using Microsoft.AspNetCore.Identity;

    public static class IdentityResultExtensions
    {
        public static void ThrowIfFailed(this IdentityResult result)
        {
            if (result.Succeeded)
                return;

            var messages = result.Errors
                .Select(MapIdentityError)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            throw new BadRequestException(
                messages.Length == 0
                    ? "Əməliyyat tamamlanmadı. Daxil etdiyiniz məlumatları yoxlayın."
                    : string.Join(" ", messages));
        }

        private static string MapIdentityError(IdentityError error) =>
            error.Code switch
            {
                "DuplicateEmail" => "Bu email ünvanı artıq istifadə olunur.",
                "DuplicateUserName" => "Bu istifadəçi adı artıq istifadə olunur.",
                "InvalidEmail" => "Email ünvanının formatı düzgün deyil.",
                "InvalidUserName" => "İstifadəçi adında icazə verilməyən simvollar var.",
                "PasswordTooShort" => "Şifrə tələb olunan minimum uzunluğa uyğun deyil.",
                "PasswordRequiresDigit" => "Şifrədə ən azı bir rəqəm olmalıdır.",
                "PasswordRequiresUpper" => "Şifrədə ən azı bir böyük hərf olmalıdır.",
                "PasswordRequiresLower" => "Şifrədə ən azı bir kiçik hərf olmalıdır.",
                "PasswordRequiresNonAlphanumeric" => "Şifrədə ən azı bir xüsusi simvol olmalıdır.",
                "PasswordMismatch" => "Cari şifrə yanlışdır.",
                "InvalidToken" => "Təsdiq linki etibarlı deyil və ya vaxtı bitib.",
                "UserAlreadyHasPassword" => "Bu hesab üçün artıq şifrə təyin olunub.",
                "LoginAlreadyAssociated" => "Bu giriş məlumatı başqa hesaba bağlıdır.",
                "UserLockoutNotEnabled" => "Bu hesab üçün müvəqqəti kilidləmə aktiv deyil.",
                _ => "Əməliyyat tamamlanmadı. Daxil etdiyiniz məlumatları yoxlayın."
            };
    }
}
