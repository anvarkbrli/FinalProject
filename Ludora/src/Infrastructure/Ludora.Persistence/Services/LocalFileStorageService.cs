namespace Ludora.Persistence.Services;

using Ludora.Application.DTOs;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Ludora.Persistence.Extensions;

public class LocalFileStorageService : IFileStorageService
{
    private const int MaxProfileImageSize = 5;

    private readonly FileStorageSettings _settings;

    public LocalFileStorageService(FileStorageSettings settings)
    {
        _settings = settings;
    }

    public async Task<string> SaveProfileImageAsync(ProfileImageUploadDTO dto)
    {
        dto.ValidateUpload(MaxProfileImageSize, FileSize.MB);

        var fileBytes = await dto.Stream.ToByteArrayAsync();

        fileBytes.ValidateActualSize(MaxProfileImageSize, FileSize.MB);

        var extension = fileBytes.GetImageExtension();

        var folderPath = Path.Combine(_settings.WebRootPath, "uploads", "profile-images");

        Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folderPath, fileName);

        await File.WriteAllBytesAsync(fullPath, fileBytes);

        return $"/uploads/profile-images/{fileName}";
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

}
