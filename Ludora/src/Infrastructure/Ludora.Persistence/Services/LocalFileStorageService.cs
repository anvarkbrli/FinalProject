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

        var fullPath = GetSafeFullPath(relativePath);

        if (fullPath is not null && File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private string? GetSafeFullPath(string relativePath)
    {
        var safeRelativePath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

        var webRootPath = Path.GetFullPath(_settings.WebRootPath);
        var fullPath = Path.GetFullPath(Path.Combine(webRootPath, safeRelativePath));
        var allowedRoot = webRootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
    }
}