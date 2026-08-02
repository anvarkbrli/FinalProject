namespace Ludora.Application.DTOs;

public record UserSettingsDTO
{
    public bool IsPrivate { get; set; }

    public string PreferredLanguage { get; set; } = "az";

    public string PreferredTheme { get; set; } = "dark";
}
