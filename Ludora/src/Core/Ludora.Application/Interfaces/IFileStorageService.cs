using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Chat;

namespace Ludora.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveProfileImageAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default);
    Task<string> SavePostImageAsync(
        PostImageUploadDTO dto,
        CancellationToken cancellationToken = default);
    Task<string> SaveReportEvidenceImageAsync(
        UserReportEvidenceUploadDTO dto,
        CancellationToken cancellationToken = default);
    Task<StoredFileDTO> ReadReportEvidenceImageAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
    Task DeleteReportEvidenceImageAsync(string? storageKey);
    Task<(string StorageKey, string ContentType, long Length)> SaveChatMediaAsync(
        ChatMediaUploadDTO dto,
        CancellationToken cancellationToken = default);
    Task<StoredChatMediaDTO> OpenChatMediaAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
    Task DeleteChatMediaAsync(string? storageKey);
    Task DeleteAsync(string? relativePath);
}
