namespace Ludora.Application.DTOs;

public record PostCreateDTO
{
    public string? Content { get; set; }
    public PostImageUploadDTO? Image { get; set; }
}
