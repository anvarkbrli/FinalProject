namespace Ludora.Application.DTOs;

public record UpdateUserSettingsDTO
{
    public bool? IsPrivate { get; set; }
}
