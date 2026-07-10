namespace Ludora.Application.Extensions;

public static class StringExtension
{
    public static string? TrimOrNull(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}