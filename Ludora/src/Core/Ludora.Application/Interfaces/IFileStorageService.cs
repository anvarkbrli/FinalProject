using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveProfileImageAsync(ProfileImageUploadDTO dto);
    Task<string> SavePostImageAsync(PostImageUploadDTO dto);
    Task<string> SaveReportEvidenceImageAsync(UserReportEvidenceUploadDTO dto);
    Task<StoredFileDTO> ReadReportEvidenceImageAsync(string storageKey);
    Task DeleteReportEvidenceImageAsync(string? storageKey);
    Task DeleteAsync(string? relativePath);
}
