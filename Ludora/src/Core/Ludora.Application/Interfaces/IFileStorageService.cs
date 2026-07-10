using Ludora.Application.DTOs;


namespace Ludora.Application.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveProfileImageAsync(ProfileImageUploadDTO DTO);

        Task DeleteAsync(string? relativePath);
    }
}
