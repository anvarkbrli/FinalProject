using Ludora.Application.DTOs;
using Ludora.Application.DTOs.FpsEstimator;
using Ludora.Application.DTOs.Home;

namespace Ludora.Application.Interfaces
{
    public interface IUserService
    {
        Task<CurrentUserDTO> GetCurrentUserAsync(Guid userId);
        Task<UserProfileDTO> GetByUserNameAsync(string userName, Guid? currentUserId);
        Task<CurrentUserDTO> UpdateProfileAsync(Guid userId, UpdateProfileDTO dto);
        Task<CurrentUserDTO> UpdateProfileImageAsync(Guid userId, ProfileImageUploadDTO dto);
        Task<UserSettingsDTO> GetSettingsAsync(Guid userId);
        Task<UserSettingsDTO> UpdateSettingsAsync(Guid userId, UpdateUserSettingsDTO dto);
        Task<ForumInterestProfileDTO?> GetForumInterestsAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
        Task<ForumInterestProfileDTO> UpdateForumInterestsAsync(
            Guid userId,
            UpdateForumInterestProfileDTO dto,
            CancellationToken cancellationToken = default);
        Task<HomeInterestRecommendationsDTO?> GetSavedHomeInterestRecommendationsAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
        Task<HomeInterestRecommendationsDTO> SaveHomeInterestRecommendationsOnceAsync(
            Guid userId,
            HomeInterestRecommendationsDTO recommendations,
            CancellationToken cancellationToken = default);
        Task<CurrentUserDTO> UpdatePreferredLanguageAsync(
            Guid userId,
            UpdatePreferredLanguageDTO dto,
            CancellationToken cancellationToken = default);
        Task<FpsHardwareProfileDTO> GetFpsHardwareProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
        Task<FpsHardwareProfileDTO> UpdateFpsHardwareProfileAsync(
            Guid userId,
            UpdateFpsHardwareProfileDTO dto,
            CancellationToken cancellationToken = default);

        Task<EmailOperationResultDTO> ChangePasswordAsync(
            Guid userId,
            ChangePasswordDTO dto);

        Task<EmailOperationResultDTO> RequestEmailChangeAsync(
            Guid userId,
            ChangeEmailRequestDTO dto,
            CancellationToken cancellationToken = default);

        Task<EmailOperationResultDTO> ConfirmEmailChangeAsync(
            Guid userId,
            string newEmail,
            string token,
            CancellationToken cancellationToken = default);
    }
}
