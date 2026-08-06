using System.Globalization;
using System.Text;

namespace Ludora.Infrastructure.FpsResearch
{
    public static class HardwareRecommendationFilter
    {
        private static readonly string[] BlockedTerms =
        [
            "gpu-nu dəyiş",
            "gpu nu dəyiş",
            "gpu dəyiş",
            "cpu-nu dəyiş",
            "cpu nu dəyiş",
            "cpu dəyiş",
            "ram artır",
            "ram artir",
            "videokartı dəyiş",
            "video kartı dəyiş",
            "prosessoru dəyiş",
            "yeni videokart",
            "yeni video kart",
            "yeni prosessor",
            "yeni kompüter al",
            "daha güclü komponent",
            "donanımı yükselt",
            "ekran kartını değiştir",
            "işlemciyi değiştir",
            "ram yükselt",
            "yeni bilgisayar al",
            "upgrade your",
            "replace your",
            "buy a new",
            "purchase a",
            "upgrade gpu",
            "upgrade cpu",
            "upgrade ram",
            "more ram",
            "hardware upgrade",
            "replace the gpu",
            "replace the cpu",
            "new graphics card",
            "new processor",
            "new computer"
        ];

        private static readonly string[] NormalizedBlockedTerms =
            BlockedTerms.Select(NormalizeForComparison).ToArray();

        private static readonly string[] HardwareTerms =
        [
            "gpu",
            "cpu",
            "ram",
            "videokart",
            "video kart",
            "prosessor",
            "ekran kart",
            "islemci",
            "hardware",
            "donanim",
            "komponent",
            "component",
            "graphics card",
            "processor",
            "computer"
        ];

        private static readonly string[] ChangeIntentTerms =
        [
            "deyis",
            "artir",
            "yukselt",
            "upgrade",
            "replace",
            "buy",
            "purchase",
            "satin al",
            "yenisini al",
            "yeni ",
            "new ",
            "more ",
            "better ",
            "stronger ",
            "more powerful",
            "daha guclu"
        ];

        public static IReadOnlyCollection<string> RemoveHardwareRecommendations(
            IEnumerable<string> recommendations)
        {
            ArgumentNullException.ThrowIfNull(recommendations);

            return recommendations
                .Where(recommendation =>
                    !string.IsNullOrWhiteSpace(recommendation) &&
                    !ContainsBlockedTerm(recommendation))
                .ToList();
        }

        public static bool ContainsBlockedTerm(string recommendation)
        {
            if (string.IsNullOrWhiteSpace(recommendation))
                return false;

            var normalized = NormalizeForComparison(recommendation);

            if (NormalizedBlockedTerms.Any(normalized.Contains))
                return true;

            return HardwareTerms.Any(normalized.Contains) &&
                   ChangeIntentTerms.Any(normalized.Contains);
        }

        private static string NormalizeForComparison(string value)
        {
            var decomposed = value
                .Normalize(NormalizationForm.FormKD)
                .ToLowerInvariant();
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                    UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(character switch
                {
                    'ə' => 'e',
                    'ı' => 'i',
                    '–' or '—' or '‑' => '-',
                    _ => character
                });
            }

            return builder
                .ToString()
                .Normalize(NormalizationForm.FormKC);
        }
    }
}
