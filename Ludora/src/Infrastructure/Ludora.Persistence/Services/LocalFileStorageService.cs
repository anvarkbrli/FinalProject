namespace Ludora.Persistence.Services;

using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;

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
        if (dto.IsEmpty()) throw new BadRequestException("Profil şəkli seçilməyib.");

        if (dto.ExceedsSize(MaxProfileImageSize, FileSize.MB))throw new BadRequestException("Profil şəkli maksimum 5 MB ola bilər.");

        await using var memoryStream = new MemoryStream();

        await dto.Stream.CopyToAsync(memoryStream);

        var fileBytes = memoryStream.ToArray();

        if (fileBytes.Length == 0)
            throw new BadRequestException("Yüklənən fayl boşdur.");

        if (fileBytes.ExceedsSize(MaxProfileImageSize, FileSize.MB))
            throw new BadRequestException("Profil şəkli maksimum 5 MB ola bilər.");

        var extension = fileBytes.DetectImageExtension();

        if (extension is null)
        {
            throw new BadRequestException(
                "Yalnız JPEG, PNG və WEBP şəkilləri qəbul edilir.");
        }

        var folderPath = Path.Combine(
            _settings.WebRootPath,
            "uploads",
            "profile-images");

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
        var safeRelativePath = relativePath
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);

        var webRootPath = Path.GetFullPath(_settings.WebRootPath);
        var fullPath = Path.GetFullPath(Path.Combine(webRootPath, safeRelativePath));

        var allowedRoot = webRootPath.TrimEnd(
            Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase)? fullPath : null;
    }
}