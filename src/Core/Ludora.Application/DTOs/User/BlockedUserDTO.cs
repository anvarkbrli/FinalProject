namespace Ludora.Application.DTOs
{
    public record BlockedUserDTO
    {
        public Guid Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? ProfileImageUrl { get; set; }

        public DateTime BlockedAt { get; set; }
    }
}
