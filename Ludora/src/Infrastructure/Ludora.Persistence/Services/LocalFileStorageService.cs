namespace Ludora.Persistence.Services;

using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Chat;
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
    private const int MaxReportEvidenceVideoSize = 50;
    private const long MaxChatImageBytes = 10L * 1024 * 1024;
    private const long MaxChatVideoBytes = 50L * 1024 * 1024;
    private const long MaxDiscussionImageBytes = 10L * 1024 * 1024;
    private const long MaxDiscussionVideoBytes = 100L * 1024 * 1024;

    private readonly FileStorageSettings _settings;

    public LocalFileStorageService(FileStorageSettings settings)
    {
        _settings = settings;
    }

    public async Task<string> SaveProfileImageAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxProfileImageSize, FileSize.MB);

        return await SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            MaxProfileImageSize,
            "profile-images",
            cancellationToken);
    }

    public async Task<string> SavePostImageAsync(
        PostImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxPostImageSize, FileSize.MB);

        return await SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            MaxPostImageSize,
            "post-images",
            cancellationToken);
    }

    public async Task<string> SaveCommunityIconAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxProfileImageSize, FileSize.MB);

        return await SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            MaxProfileImageSize,
            "community-icons",
            cancellationToken);
    }

    public async Task<string> SaveCommunityCoverImageAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxPostImageSize, FileSize.MB);

        return await SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            MaxPostImageSize,
            "community-covers",
            cancellationToken);
    }

    public async Task<string> SaveBadgeIconAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxProfileImageSize, FileSize.MB);

        return await SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            MaxProfileImageSize,
            "badge-icons",
            cancellationToken);
    }

    public async Task<string> SaveReportEvidenceImageAsync(
        UserReportEvidenceUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Stream == Stream.Null || dto.Length <= 0)
            throw new BadRequestException("Şikayət sübutu boşdur.");

        var declaredType = dto.ContentType.Trim().ToLowerInvariant();
        var isVideo = declaredType.StartsWith(
            "video/",
            StringComparison.Ordinal);
        var isImage = declaredType.StartsWith(
            "image/",
            StringComparison.Ordinal);

        if (!isImage && !isVideo)
        {
            throw new BadRequestException(
                "Sübut kimi yalnız şəkil və video faylları qəbul edilir.");
        }

        var maxLength = ToBytes(
            isVideo
                ? MaxReportEvidenceVideoSize
                : MaxReportEvidenceImageSize);

        if (dto.Length > maxLength)
        {
            throw new BadRequestException(
                isVideo
                    ? $"Sübut videosu maksimum {MaxReportEvidenceVideoSize} MB ola bilər."
                    : $"Sübut şəkli maksimum {MaxReportEvidenceImageSize} MB ola bilər.");
        }

        var folderName = "report-evidence";
        var folderPath = Path.Combine(
            _settings.PrivateStoragePath,
            folderName);
        Directory.CreateDirectory(folderPath);

        var temporaryPath = Path.Combine(
            folderPath,
            $"{Guid.NewGuid():N}.uploading");
        long written = 0;
        var header = new byte[16];
        var headerLength = 0;

        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                int read;

                while ((read = await dto.Stream.ReadAsync(
                           buffer,
                           cancellationToken)) > 0)
                {
                    written += read;
                    if (written > maxLength)
                    {
                        throw new BadRequestException(
                            "Sübut faylı icazə verilən ölçünü keçir.");
                    }

                    if (headerLength < header.Length)
                    {
                        var copyLength = Math.Min(
                            header.Length - headerLength,
                            read);
                        Buffer.BlockCopy(
                            buffer,
                            0,
                            header,
                            headerLength,
                            copyLength);
                        headerLength += copyLength;
                    }

                    await output.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken);
                }
            }

            if (written == 0 || written != dto.Length)
            {
                throw new BadRequestException(
                    "Sübut faylının ölçüsü doğrulana bilmədi.");
            }

            var detected = DetectChatMedia(
                header.AsSpan(0, headerLength));
            if (detected.ContentType is null)
            {
                throw new BadRequestException(
                    "Yalnız JPEG, PNG, WEBP, MP4 və WEBM formatları dəstəklənir.");
            }

            if (!string.Equals(
                    detected.ContentType,
                    declaredType,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "Sübut faylının real formatı Content-Type ilə uyğun deyil.");
            }

            var fileName = $"{Guid.NewGuid():N}{detected.Extension}";
            var finalPath = Path.Combine(folderPath, fileName);
            File.Move(temporaryPath, finalPath);

            return $"{folderName}/{fileName}";
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }
    }

    public async Task<StoredFileDTO> ReadReportEvidenceImageAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
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
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => throw new BadRequestException(
                "Şikayət sübutunun fayl formatı düzgün deyil.")
        };

        return new StoredFileDTO
        {
            Content = await File.ReadAllBytesAsync(
                fullPath,
                cancellationToken),
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

    public async Task<(string StorageKey, string ContentType, long Length)> SaveChatMediaAsync(
        ChatMediaUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Length <= 0)
            throw new BadRequestException("Göndərilən media faylı boşdur.");

        var declaredType = dto.ContentType.Trim().ToLowerInvariant();
        var maxLength = declaredType.StartsWith("video/", StringComparison.Ordinal)
            ? MaxChatVideoBytes
            : MaxChatImageBytes;

        if (dto.Length > maxLength)
        {
            throw new BadRequestException(
                declaredType.StartsWith("video/", StringComparison.Ordinal)
                    ? "Video maksimum 50 MB ola bilər."
                    : "Şəkil maksimum 10 MB ola bilər.");
        }

        var folderName = "chat-media";
        var folderPath = Path.Combine(_settings.PrivateStoragePath, folderName);
        Directory.CreateDirectory(folderPath);

        var temporaryPath = Path.Combine(folderPath, $"{Guid.NewGuid():N}.uploading");
        long written = 0;
        var header = new byte[16];
        var headerLength = 0;

        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                int read;

                while ((read = await dto.Stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    if (written > maxLength)
                        throw new BadRequestException("Media faylı icazə verilən ölçünü keçir.");

                    if (headerLength < header.Length)
                    {
                        var copyLength = Math.Min(header.Length - headerLength, read);
                        Buffer.BlockCopy(buffer, 0, header, headerLength, copyLength);
                        headerLength += copyLength;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            if (written == 0 || written != dto.Length)
                throw new BadRequestException("Media faylının ölçüsü doğrulana bilmədi.");

            var detected = DetectChatMedia(header.AsSpan(0, headerLength));
            if (detected.ContentType is null ||
                !string.Equals(detected.ContentType, declaredType, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "Media faylının real formatı göndərilən Content-Type ilə uyğun deyil.");
            }

            var fileName = $"{Guid.NewGuid():N}{detected.Extension}";
            var finalPath = Path.Combine(folderPath, fileName);
            File.Move(temporaryPath, finalPath);

            return ($"{folderName}/{fileName}", detected.ContentType, written);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }
    }

    public Task<StoredChatMediaDTO> OpenChatMediaAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = _settings.PrivateStoragePath.GetSafeFullPath(storageKey);

        if (fullPath is null || !File.Exists(fullPath))
            throw new NotFoundException("Mesaj mediası tapılmadı.");

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => throw new BadRequestException("Mesaj mediasının formatı dəstəklənmir.")
        };

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(new StoredChatMediaDTO
        {
            Content = stream,
            ContentType = contentType,
            FileName = Path.GetFileName(fullPath)
        });
    }

    public Task DeleteChatMediaAsync(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return Task.CompletedTask;

        var fullPath = _settings.PrivateStoragePath.GetSafeFullPath(storageKey);
        if (fullPath is not null && File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public async Task<(string Url, string ContentType, long Length)> SaveDiscussionMediaAsync(
        DiscussionMediaUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Stream == Stream.Null || dto.Length <= 0)
            throw new BadRequestException("Müzakirə mediası boşdur.");

        var declaredType = dto.ContentType.Trim().ToLowerInvariant();
        var isVideo = declaredType.StartsWith("video/", StringComparison.Ordinal);
        var maxLength = isVideo
            ? MaxDiscussionVideoBytes
            : MaxDiscussionImageBytes;

        if (!isVideo &&
            !declaredType.StartsWith("image/", StringComparison.Ordinal))
        {
            throw new BadRequestException(
                "Müzakirəyə yalnız şəkil və video faylları əlavə edilə bilər.");
        }

        if (dto.Length > maxLength)
        {
            throw new BadRequestException(
                isVideo
                    ? "Hər video maksimum 100 MB ola bilər."
                    : "Hər şəkil maksimum 10 MB ola bilər.");
        }

        var folderPath = Path.Combine(
            _settings.WebRootPath,
            "uploads",
            "discussion-media");
        Directory.CreateDirectory(folderPath);

        var temporaryPath = Path.Combine(
            folderPath,
            $"{Guid.NewGuid():N}.uploading");
        long written = 0;
        var header = new byte[16];
        var headerLength = 0;

        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                int read;

                while ((read = await dto.Stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    if (written > maxLength)
                        throw new BadRequestException(
                            "Media faylı icazə verilən ölçünü keçir.");

                    if (headerLength < header.Length)
                    {
                        var copyLength = Math.Min(header.Length - headerLength, read);
                        Buffer.BlockCopy(buffer, 0, header, headerLength, copyLength);
                        headerLength += copyLength;
                    }

                    await output.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken);
                }
            }

            if (written == 0 || written != dto.Length)
                throw new BadRequestException(
                    "Media faylının ölçüsü doğrulana bilmədi.");

            var detected = DetectChatMedia(header.AsSpan(0, headerLength));
            if (detected.ContentType is null)
                throw new BadRequestException(
                    "Yalnız JPEG, PNG, WEBP, MP4 və WEBM formatları dəstəklənir.");

            if (!string.Equals(
                    detected.ContentType,
                    declaredType,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "Media faylının real formatı Content-Type ilə uyğun deyil.");
            }

            var fileName = $"{Guid.NewGuid():N}{detected.Extension}";
            var finalPath = Path.Combine(folderPath, fileName);
            File.Move(temporaryPath, finalPath);

            return (
                $"/uploads/discussion-media/{fileName}",
                detected.ContentType,
                written);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }
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
        long declaredLength,
        int maxSizeInMb,
        string folderName,
        CancellationToken cancellationToken)
    {
        var fileBytes = await stream.ToByteArrayAsync(
            declaredLength,
            ToBytes(maxSizeInMb),
            cancellationToken);
        fileBytes.ValidateActualSize(maxSizeInMb, FileSize.MB);

        var extension = fileBytes.GetImageExtension();
        var folderPath = Path.Combine(
            _settings.WebRootPath,
            "uploads",
            folderName);

        Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folderPath, fileName);

        await File.WriteAllBytesAsync(
            fullPath,
            fileBytes,
            cancellationToken);

        return $"/uploads/{folderName}/{fileName}";
    }

    private static long ToBytes(int sizeInMb)
    {
        return checked(sizeInMb * 1024L * 1024L);
    }

    private static (string? ContentType, string Extension) DetectChatMedia(
        ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 &&
            header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
            return ("image/jpeg", ".jpg");

        if (header.Length >= 8 &&
            header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return ("image/png", ".png");

        if (header.Length >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) &&
            header.Slice(8, 4).SequenceEqual("WEBP"u8))
            return ("image/webp", ".webp");

        if (header.Length >= 12 &&
            header.Slice(4, 4).SequenceEqual("ftyp"u8))
            return ("video/mp4", ".mp4");

        if (header.Length >= 4 &&
            header[..4].SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }))
            return ("video/webm", ".webm");

        return (null, string.Empty);
    }
}
