namespace Ludora.Application.Extensions
{
    using Ludora.Application.Exceptions;

    public static class StringExtensions
    {
        public static string? TrimOrNull(this string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static string NormalizeRequiredText(
            this string? value,
            string fieldName,
            int maximumLength)
        {
            var normalizedValue = value.TrimOrNull();

            if (normalizedValue is null)
                throw new BadRequestException($"{fieldName} boş ola bilməz.");

            if (normalizedValue.Length > maximumLength)
            {
                throw new BadRequestException(
                    $"{fieldName} maksimum {maximumLength} simvol ola bilər.");
            }

            return normalizedValue;
        }

        public static string? NormalizeOptionalText(
            this string? value,
            string fieldName,
            int maximumLength)
        {
            var normalizedValue = value.TrimOrNull();

            if (normalizedValue is null)
                return null;

            if (normalizedValue.Length > maximumLength)
            {
                throw new BadRequestException(
                    $"{fieldName} maksimum {maximumLength} simvol ola bilər.");
            }

            return normalizedValue;
        }
    }
}
