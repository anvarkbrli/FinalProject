namespace Ludora.Application.DTOs.Notification
{
    public sealed record NotificationSettingsDTO
    {
        public IReadOnlyList<NotificationPreferenceDTO> Items { get; init; } = [];
    }
}
