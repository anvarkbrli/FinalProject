namespace Ludora.Application.DTOs.Contact
{
    public sealed record ContactMessageCreateDTO
    {
        public string? Name { get; init; }
        public string? Email { get; init; }
        public string? Subject { get; init; }
        public string? Message { get; init; }
        public string? Language { get; init; }
    }
}
