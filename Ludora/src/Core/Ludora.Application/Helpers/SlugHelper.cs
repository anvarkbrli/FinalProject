using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ludora.Application.Helpers
{
    public static class SlugHelper
    {
        public static string Generate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "game";
            }

            var text = value
                .Trim()
                .ToLowerInvariant()
                .Replace('ə', 'e')
                .Replace('ı', 'i')
                .Replace('ş', 's')
                .Replace('ç', 'c')
                .Replace('ö', 'o')
                .Replace('ü', 'u')
                .Replace('ğ', 'g');

            text = RemoveDiacritics(text);

            text = Regex.Replace(text, @"[^a-z0-9]+", "-");

            text = text.Trim('-');

            return string.IsNullOrWhiteSpace(text) ? "game" : text;
        }

        private static string RemoveDiacritics(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);

            var result = new StringBuilder();

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);

                if (category != UnicodeCategory.NonSpacingMark)
                {
                    result.Append(character);
                }
            }

            return result.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
