using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IUserService
    {
        Task<CurrentUserDTO> GetCurrentUserAsync(Guid userId);
        Task<UserProfileDTO> GetByUserNameAsync(string userName);
        Task<CurrentUserDTO> UpdateProfileAsync(Guid userId, UpdateProfileDTO DTO);
        Task<CurrentUserDTO> UpdateProfileImageAsync(Guid userId, ProfileImageUploadDTO DTO);


    }
}
