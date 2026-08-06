namespace Ludora.Application.DTOs
{
    public record ChangeUserRoleDTO
    {
        public string Role { get; init; } = string.Empty;
    }
}
