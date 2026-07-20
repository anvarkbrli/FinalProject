using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveProfileImageAsync(ProfileImageUploadDTO dto);
    Task<string> SavePostImageAsync(PostImageUploadDTO dto);
    Task DeleteAsync(string? relativePath);
}
