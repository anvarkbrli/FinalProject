namespace Ludora.Application.DTOs
{
    public sealed record UpdatePreferredLanguageDTO
    {
        public string Language { get; init; } = string.Empty;
    }
}
