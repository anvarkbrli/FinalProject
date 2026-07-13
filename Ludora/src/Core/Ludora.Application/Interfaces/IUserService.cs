using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IUserService
    {
        Task<CurrentUserDTO> GetCurrentUserAsync(Guid userId);
        Task<UserProfileDTO> GetByUserNameAsync(string userName, Guid? currentUserId);
        Task<CurrentUserDTO> UpdateProfileAsync(Guid userId, UpdateProfileDTO DTO);
        Task<CurrentUserDTO> UpdateProfileImageAsync(Guid userId, ProfileImageUploadDTO DTO);
        Task<UserSettingsDTO> GetSettingsAsync(Guid userId);
        Task<UserSettingsDTO> UpdateSettingsAsync(Guid userId, UpdateUserSettingsDTO dto);


    }
}
