using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Notification;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces
{
    public interface INotificationService
    {
        Task<PagedResultDTO<NotificationDTO>> GetMineAsync(
            Guid userId,
            NotificationFilterDTO filter,
            CancellationToken cancellationToken = default);

        Task<UnreadNotificationCountDTO> GetUnreadCountAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<NotificationDTO> MarkAsReadAsync(
            Guid userId,
            long notificationId,
            CancellationToken cancellationToken = default);

        Task MarkAllAsReadAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            Guid userId,
            long notificationId,
            CancellationToken cancellationToken = default);

        Task<NotificationSettingsDTO> GetSettingsAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<NotificationPreferenceDTO> UpdatePreferenceAsync(
            Guid userId,
            NotificationType type,
            UpdateNotificationPreferenceDTO dto,
            CancellationToken cancellationToken = default);

        Task ResetPreferencesAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task QueueAsync(
            NotificationCreateRequest request,
            CancellationToken cancellationToken = default);

        Task QueueForFollowersAsync(
            FollowerNotificationRequest request,
            CancellationToken cancellationToken = default);

        Task QueueAnnouncementAsync(
            Guid adminUserId,
            int announcementId,
            string title,
            string content,
            AnnouncementAudience audience,
            CancellationToken cancellationToken = default);
    }
}
