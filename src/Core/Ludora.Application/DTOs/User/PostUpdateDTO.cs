namespace Ludora.Application.DTOs
{
    public record PostUpdateDTO
    {
        public string? Content { get; set; }
        public PostImageUploadDTO? Image { get; set; }
        public bool RemoveImage { get; set; }
    }
}
