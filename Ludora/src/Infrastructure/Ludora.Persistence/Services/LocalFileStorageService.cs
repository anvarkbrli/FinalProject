namespace Ludora.Persistence.Services;

using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Ludora.Persistence.Extensions;

public class LocalFileStorageService : IFileStorageService
{
    private const int MaxProfileImageSize = 5;
    private const int MaxPostImageSize = 10;
    private const int MaxReportEvidenceImageSize = 5;

    private readonly FileStorageSettings _settings;

    public LocalFileStorageService(FileStorageSettings settings)
    {
        _settings = settings;
    }

    public async Task<string> SaveProfileImageAsync(ProfileImageUploadDTO dto)
    {
        dto.ValidateUpload(MaxProfileImageSize, FileSize.MB);

        return await SavePublicImageAsync(
            dto.Stream,
            MaxProfileImageSize,
            "profile-images");
    }

    public async Task<string> SavePostImageAsync(PostImageUploadDTO dto)
    {
        dto.ValidateUpload(MaxPostImageSize, FileSize.MB);

        return await SavePublicImageAsync(
            dto.Stream,
            MaxPostImageSize,
            "post-images");
    }

    public async Task<string> SaveReportEvidenceImageAsync(
        UserReportEvidenceUploadDTO dto)
    {
        dto.ValidateUpload(MaxReportEvidenceImageSize, FileSize.MB);

        var fileBytes = await dto.Stream.ToByteArrayAsync();
        fileBytes.ValidateActualSize(
            MaxReportEvidenceImageSize,
            FileSize.MB);

        var extension = fileBytes.GetImageExtension();
        var folderName = "report-evidence";
        var folderPath = Path.Combine(
            _settings.PrivateStoragePath,
            folderName);

        Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folderPath, fileName);

        await File.WriteAllBytesAsync(fullPath, fileBytes);

        return $"{folderName}/{fileName}";
    }

    public async Task<StoredFileDTO> ReadReportEvidenceImageAsync(
        string storageKey)
    {
        var fullPath = _settings.PrivateStoragePath
            .GetSafeFullPath(storageKey);

        if (fullPath is null || !File.Exists(fullPath))
            throw new NotFoundException("Şikayət sübutu tapılmadı.");

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new BadRequestException(
                "Şikayət sübutunun fayl formatı düzgün deyil.")
        };

        return new StoredFileDTO
        {
            Content = await File.ReadAllBytesAsync(fullPath),
            ContentType = contentType,
            FileName = Path.GetFileName(fullPath)
        };
    }

    public Task DeleteReportEvidenceImageAsync(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return Task.CompletedTask;

        var fullPath = _settings.PrivateStoragePath
            .GetSafeFullPath(storageKey);

        if (fullPath is not null && File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Task.CompletedTask;

        var fullPath = _settings.WebRootPath.GetSafeFullPath(relativePath);

        if (fullPath is not null && File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private async Task<string> SavePublicImageAsync(
        Stream stream,
        int maxSizeInMb,
        string folderName)
    {
        var fileBytes = await stream.ToByteArrayAsync();
        fileBytes.ValidateActualSize(maxSizeInMb, FileSize.MB);

        var extension = fileBytes.GetImageExtension();
        var folderPath = Path.Combine(
            _settings.WebRootPath,
            "uploads",
            folderName);

        Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folderPath, fileName);

        await File.WriteAllBytesAsync(fullPath, fileBytes);

        return $"/uploads/{folderName}/{fileName}";
    }
}
