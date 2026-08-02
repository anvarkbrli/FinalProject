namespace Ludora.Application.DTOs;

public record UpdateUserSettingsDTO
{
    public bool? IsPrivate { get; set; }

    public string? PreferredLanguage { get; set; }

    public string? PreferredTheme { get; set; }
}
