namespace Ludora.Persistence.Services;

using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Chat;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Application.Settings;
using Ludora.Persistence.Extensions;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

public class LocalFileStorageService : IFileStorageService
{
    private const int MaxProfileImageSize = 5;
    private const int MaxPostImageSize = 10;
    private const int MaxGameCoverImageSize = 10;
    private const int MaxReportEvidenceImageSize = 5;
    private const int MaxReportEvidenceVideoSize = 50;
    private const long MaxChatImageBytes = 10L * 1024 * 1024;
    private const long MaxChatVideoBytes = 50L * 1024 * 1024;
    private const long MaxDiscussionImageBytes = 10L * 1024 * 1024;
    private const long MaxDiscussionVideoBytes = 100L * 1024 * 1024;
    private const long MaxDecodedPixels = 40_000_000;
    private const int MaxImageDimension = 12_000;

    private readonly FileStorageSettings _settings;
    private readonly IMalwareScanner _malwareScanner;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        FileStorageSettings settings,
        IMalwareScanner malwareScanner,
        ILogger<LocalFileStorageService> logger)
    {
        _settings = settings;
        _malwareScanner = malwareScanner;
        _logger = logger;
    }

    public Task<string> SaveProfileImageAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxProfileImageSize, FileSize.MB);
        return SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            ToBytes(MaxProfileImageSize),
            dto.FileName,
            dto.ContentType,
            "profile-images",
            cancellationToken);
    }

    public Task<string> SavePostImageAsync(
        PostImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxPostImageSize, FileSize.MB);
        return SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            ToBytes(MaxPostImageSize),
            dto.FileName,
            dto.ContentType,
            "post-images",
            cancellationToken);
    }

    public Task<string> SaveGameCoverImageAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxGameCoverImageSize, FileSize.MB);
        return SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            ToBytes(MaxGameCoverImageSize),
            dto.FileName,
            dto.ContentType,
            "game-covers",
            cancellationToken);
    }

    public Task<string> SaveCommunityIconAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxProfileImageSize, FileSize.MB);
        return SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            ToBytes(MaxProfileImageSize),
            dto.FileName,
            dto.ContentType,
            "community-icons",
            cancellationToken);
    }

    public Task<string> SaveCommunityCoverImageAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxPostImageSize, FileSize.MB);
        return SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            ToBytes(MaxPostImageSize),
            dto.FileName,
            dto.ContentType,
            "community-covers",
            cancellationToken);
    }

    public Task<string> SaveBadgeIconAsync(
        ProfileImageUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        dto.ValidateUpload(MaxProfileImageSize, FileSize.MB);
        return SavePublicImageAsync(
            dto.Stream,
            dto.Length,
            ToBytes(MaxProfileImageSize),
            dto.FileName,
            dto.ContentType,
            "badge-icons",
            cancellationToken);
    }

    public async Task<string> SaveReportEvidenceImageAsync(
        UserReportEvidenceUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var declaredType = NormalizeContentType(dto.ContentType);
        var isVideo = declaredType.StartsWith("video/", StringComparison.Ordinal);
        var isImage = declaredType.StartsWith("image/", StringComparison.Ordinal);

        if (!isImage && !isVideo)
            throw new BadRequestException("Sübut kimi yalnız şəkil və video faylları qəbul edilir.");

        var maxLength = ToBytes(
            isVideo ? MaxReportEvidenceVideoSize : MaxReportEvidenceImageSize);

        var quarantine = await WriteToQuarantineAsync(
            dto.Stream,
            dto.Length,
            maxLength,
            dto.FileName,
            cancellationToken);

        try
        {
            var detected = DetectMedia(quarantine.Header);
            ValidateDetectedMedia(
                detected,
                declaredType,
                "Yalnız JPEG, PNG, WEBP, MP4 və WEBM formatları dəstəklənir.");

            await ScanQuarantinedFileAsync(
                quarantine.Path,
                dto.FileName,
                cancellationToken);

            const string folderName = "report-evidence";

            if (detected.ContentType!.StartsWith("image/", StringComparison.Ordinal))
            {
                var key = $"{folderName}/{Guid.NewGuid():N}.webp";
                await NormalizeAndStoreImageAsync(
                    quarantine.Path,
                    key,
                    isPublic: false,
                    cancellationToken);
                return key;
            }

            var videoKey = $"{folderName}/{Guid.NewGuid():N}{detected.Extension}";
            await StoreExistingFileAsync(
                quarantine.Path,
                videoKey,
                isPublic: false,
                cancellationToken);
            return videoKey;
        }
        finally
        {
            DeleteIfExists(quarantine.Path);
        }
    }

    public async Task<StoredFileDTO> ReadReportEvidenceImageAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return new StoredFileDTO
            {
                Content = await ReadAllBytesAsync(
                    storageKey,
                    isPublic: false,
                    cancellationToken),
                ContentType = GetStoredContentType(storageKey),
                FileName = Path.GetFileName(storageKey)
            };
        }
        catch (NotFoundException)
        {
            throw new NotFoundException("Şikayət sübutu tapılmadı.");
        }
    }

    public Task DeleteReportEvidenceImageAsync(string? storageKey)
    {
        if (!string.IsNullOrWhiteSpace(storageKey))
            DeleteStoredFile(storageKey, isPublic: false);

        return Task.CompletedTask;
    }

    public async Task<(string StorageKey, string ContentType, long Length)> SaveChatMediaAsync(
        ChatMediaUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var declaredType = NormalizeContentType(dto.ContentType);
        var isVideo = declaredType.StartsWith("video/", StringComparison.Ordinal);
        var isImage = declaredType.StartsWith("image/", StringComparison.Ordinal);

        if (!isImage && !isVideo)
            throw new BadRequestException("Mesaja yalnız şəkil və video əlavə edilə bilər.");

        var maxLength = isVideo ? MaxChatVideoBytes : MaxChatImageBytes;
        var quarantine = await WriteToQuarantineAsync(
            dto.Stream,
            dto.Length,
            maxLength,
            dto.FileName,
            cancellationToken);

        try
        {
            var detected = DetectMedia(quarantine.Header);
            ValidateDetectedMedia(
                detected,
                declaredType,
                "Yalnız JPEG, PNG, WEBP, MP4 və WEBM formatları dəstəklənir.");

            await ScanQuarantinedFileAsync(
                quarantine.Path,
                dto.FileName,
                cancellationToken);

            const string folderName = "chat-media";

            if (detected.ContentType!.StartsWith("image/", StringComparison.Ordinal))
            {
                var key = $"{folderName}/{Guid.NewGuid():N}.webp";
                var length = await NormalizeAndStoreImageAsync(
                    quarantine.Path,
                    key,
                    isPublic: false,
                    cancellationToken);
                return (key, "image/webp", length);
            }

            var videoKey = $"{folderName}/{Guid.NewGuid():N}{detected.Extension}";
            await StoreExistingFileAsync(
                quarantine.Path,
                videoKey,
                isPublic: false,
                cancellationToken);
            return (videoKey, detected.ContentType, quarantine.Length);
        }
        finally
        {
            DeleteIfExists(quarantine.Path);
        }
    }

    public Task<StoredChatMediaDTO> OpenChatMediaAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = GetStoredFilePath(storageKey, isPublic: false);
            if (!File.Exists(fullPath))
                throw new NotFoundException("Fayl tapılmadı.");

            Stream content = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            return Task.FromResult(new StoredChatMediaDTO
            {
                Content = content,
                ContentType = GetStoredContentType(fullPath),
                FileName = Path.GetFileName(fullPath)
            });
        }
        catch (NotFoundException)
        {
            throw new NotFoundException("Mesaj mediası tapılmadı.");
        }
    }

    public Task DeleteChatMediaAsync(string? storageKey)
    {
        if (!string.IsNullOrWhiteSpace(storageKey))
            DeleteStoredFile(storageKey, isPublic: false);

        return Task.CompletedTask;
    }

    public async Task<(string Url, string ContentType, long Length)> SaveDiscussionMediaAsync(
        DiscussionMediaUploadDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var declaredType = NormalizeContentType(dto.ContentType);
        var isVideo = declaredType.StartsWith("video/", StringComparison.Ordinal);
        var isImage = declaredType.StartsWith("image/", StringComparison.Ordinal);

        if (!isImage && !isVideo)
            throw new BadRequestException("Müzakirəyə yalnız şəkil və video faylları əlavə edilə bilər.");

        var maxLength = isVideo ? MaxDiscussionVideoBytes : MaxDiscussionImageBytes;
        var quarantine = await WriteToQuarantineAsync(
            dto.Stream,
            dto.Length,
            maxLength,
            dto.FileName,
            cancellationToken);

        try
        {
            var detected = DetectMedia(quarantine.Header);
            ValidateDetectedMedia(
                detected,
                declaredType,
                "Yalnız JPEG, PNG, WEBP, MP4 və WEBM formatları dəstəklənir.");

            await ScanQuarantinedFileAsync(
                quarantine.Path,
                dto.FileName,
                cancellationToken);

            const string folderName = "discussion-media";

            if (detected.ContentType!.StartsWith("image/", StringComparison.Ordinal))
            {
                var key = $"{folderName}/{Guid.NewGuid():N}.webp";
                var length = await NormalizeAndStoreImageAsync(
                    quarantine.Path,
                    key,
                    isPublic: true,
                    cancellationToken);
                return (GetPublicUrl(key), "image/webp", length);
            }

            var videoKey = $"{folderName}/{Guid.NewGuid():N}{detected.Extension}";
            await StoreExistingFileAsync(
                quarantine.Path,
                videoKey,
                isPublic: true,
                cancellationToken);
            return (
                GetPublicUrl(videoKey),
                detected.ContentType,
                quarantine.Length);
        }
        finally
        {
            DeleteIfExists(quarantine.Path);
        }
    }

    public async Task DeleteAsync(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            !TryGetPublicKey(relativePath, out var key))
        {
            return;
        }

        DeleteStoredFile(key, isPublic: true);
        await Task.CompletedTask;
    }

    private async Task<string> SavePublicImageAsync(
        Stream stream,
        long declaredLength,
        long maxLength,
        string originalFileName,
        string declaredContentType,
        string folderName,
        CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeContentType(declaredContentType);
        if (!normalizedType.StartsWith("image/", StringComparison.Ordinal))
            throw new BadRequestException("Yalnız şəkil faylları qəbul edilir.");

        var quarantine = await WriteToQuarantineAsync(
            stream,
            declaredLength,
            maxLength,
            originalFileName,
            cancellationToken);

        try
        {
            var detected = DetectMedia(quarantine.Header);
            if (detected.ContentType is null ||
                !detected.ContentType.StartsWith("image/", StringComparison.Ordinal))
            {
                throw new BadRequestException("Yalnız JPEG, PNG və WEBP şəkilləri dəstəklənir.");
            }

            if (!string.Equals(
                    detected.ContentType,
                    normalizedType,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "Şəklin real formatı göndərilən Content-Type ilə uyğun deyil.");
            }

            await ScanQuarantinedFileAsync(
                quarantine.Path,
                originalFileName,
                cancellationToken);

            var key = $"{folderName}/{Guid.NewGuid():N}.webp";
            await NormalizeAndStoreImageAsync(
                quarantine.Path,
                key,
                isPublic: true,
                cancellationToken);

            return GetPublicUrl(key);
        }
        finally
        {
            DeleteIfExists(quarantine.Path);
        }
    }

    private async Task<long> NormalizeAndStoreImageAsync(
        string sourcePath,
        string key,
        bool isPublic,
        CancellationToken cancellationToken)
    {
        var quarantineFolder = Path.Combine(
            _settings.PrivateStoragePath,
            "quarantine");
        Directory.CreateDirectory(quarantineFolder);
        var normalizedPath = Path.Combine(
            quarantineFolder,
            $"{Guid.NewGuid():N}.normalized.webp");

        try
        {
            await NormalizeImageAsync(
                sourcePath,
                normalizedPath,
                cancellationToken);
            var length = new FileInfo(normalizedPath).Length;
            await using var stream = new FileStream(
                normalizedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await StoreStreamAsync(
                key,
                stream,
                isPublic,
                cancellationToken);
            return length;
        }
        finally
        {
            DeleteIfExists(normalizedPath);
        }
    }

    private async Task StoreExistingFileAsync(
        string sourcePath,
        string key,
        bool isPublic,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await StoreStreamAsync(
            key,
            stream,
            isPublic,
            cancellationToken);
    }

    private async Task<QuarantinedFile> WriteToQuarantineAsync(
        Stream source,
        long declaredLength,
        long maxLength,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        if (source == Stream.Null || !source.CanRead || declaredLength <= 0)
            throw new BadRequestException("Yüklənən fayl boşdur.");

        if (declaredLength > maxLength)
            throw new BadRequestException("Fayl icazə verilən ölçünü keçir.");

        var quarantineFolder = Path.Combine(
            _settings.PrivateStoragePath,
            "quarantine");
        Directory.CreateDirectory(quarantineFolder);

        var safeName = Path.GetFileName(originalFileName);
        var path = Path.Combine(quarantineFolder, $"{Guid.NewGuid():N}.uploading");
        var header = new byte[16];
        var headerLength = 0;
        long written = 0;

        try
        {
            await using var output = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = new byte[64 * 1024];
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                    break;

                written += read;
                if (written > maxLength)
                    throw new BadRequestException("Fayl icazə verilən ölçünü keçir.");

                if (headerLength < header.Length)
                {
                    var copyLength = Math.Min(header.Length - headerLength, read);
                    Buffer.BlockCopy(buffer, 0, header, headerLength, copyLength);
                    headerLength += copyLength;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            await output.FlushAsync(cancellationToken);

            if (written == 0 || written != declaredLength)
                throw new BadRequestException("Faylın ölçüsü doğrulana bilmədi.");

            return new QuarantinedFile(
                path,
                header.AsSpan(0, headerLength).ToArray(),
                written,
                safeName);
        }
        catch
        {
            DeleteIfExists(path);
            throw;
        }
    }

    private async Task ScanQuarantinedFileAsync(
        string path,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var scan = await _malwareScanner.ScanAsync(
            stream,
            Path.GetFileName(originalFileName),
            cancellationToken);

        if (scan.Status is MalwareScanStatus.Infected)
            throw new BadRequestException("Fayl təhlükəsizlik yoxlamasından keçmədi.");

        if (scan.Status is MalwareScanStatus.Skipped)
        {
            _logger.LogWarning(
                "{FileName} faylı malware skanı söndürüldüyü üçün skan edilmədi.",
                Path.GetFileName(originalFileName));
        }
    }

    private static async Task NormalizeImageAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = await Image.IdentifyAsync(sourcePath, cancellationToken)
                ?? throw new BadRequestException("Şəkil faylı oxuna bilmədi.");

            if (info.Width <= 0 || info.Height <= 0 ||
                info.Width > MaxImageDimension ||
                info.Height > MaxImageDimension ||
                (long)info.Width * info.Height > MaxDecodedPixels)
            {
                throw new BadRequestException("Şəklin ölçüləri icazə verilən həddi keçir.");
            }

            using var image = await Image.LoadAsync(sourcePath, cancellationToken);
            if (image.Frames.Count != 1)
                throw new BadRequestException("Animasiya edilmiş şəkillər dəstəklənmir.");

            image.Mutate(context => context.AutoOrient());
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            await image.SaveAsync(
                destinationPath,
                new WebpEncoder { Quality = 85 },
                cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            DeleteIfExists(destinationPath);
            throw new BadRequestException("Şəkil formatı düzgün deyil.");
        }
        catch (InvalidImageContentException)
        {
            DeleteIfExists(destinationPath);
            throw new BadRequestException("Şəkil faylı zədələnib və ya düzgün deyil.");
        }
        catch
        {
            DeleteIfExists(destinationPath);
            throw;
        }
    }

    private static void ValidateDetectedMedia(
        DetectedMedia detected,
        string declaredType,
        string unsupportedMessage)
    {
        if (detected.ContentType is null)
            throw new BadRequestException(unsupportedMessage);

        if (!string.Equals(
                detected.ContentType,
                declaredType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "Faylın real formatı göndərilən Content-Type ilə uyğun deyil.");
        }
    }

    private async Task StoreStreamAsync(
        string key,
        Stream content,
        bool isPublic,
        CancellationToken cancellationToken)
    {
        var fullPath = GetStoredFilePath(key, isPublic);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var output = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await content.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private async Task<byte[]> ReadAllBytesAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken)
    {
        var fullPath = GetStoredFilePath(key, isPublic);
        if (!File.Exists(fullPath))
            throw new NotFoundException("Fayl tapılmadı.");

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    private void DeleteStoredFile(string key, bool isPublic)
    {
        var fullPath = GetStoredFilePath(key, isPublic);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private string GetStoredFilePath(string key, bool isPublic)
    {
        var root = isPublic
            ? Path.Combine(_settings.WebRootPath, "uploads")
            : _settings.PrivateStoragePath;
        var normalizedRoot = Path.GetFullPath(root);
        var normalizedKey = NormalizeStorageKey(key);
        var fullPath = Path.GetFullPath(
            Path.Combine(
                normalizedRoot,
                normalizedKey.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Fayl yolu düzgün deyil.");
        }

        return fullPath;
    }

    private static string GetPublicUrl(string key) =>
        $"/uploads/{NormalizeStorageKey(key)}";

    private static bool TryGetPublicKey(string urlOrPath, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(urlOrPath))
            return false;

        var value = urlOrPath.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
            value = absoluteUri.AbsolutePath;

        const string prefix = "/uploads/";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            key = NormalizeStorageKey(value[prefix.Length..]);
            return key.Length > 0;
        }
        catch (BadRequestException)
        {
            key = string.Empty;
            return false;
        }
    }

    private static string NormalizeStorageKey(string key)
    {
        var normalized = key.Replace('\\', '/').Trim().Trim('/');
        var segments = normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 ||
            segments.Any(segment => segment is "." or ".."))
        {
            throw new BadRequestException("Fayl yolu düzgün deyil.");
        }

        return string.Join('/', segments);
    }

    private static string GetStoredContentType(string fullPath) =>
        Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => throw new BadRequestException("Fayl formatı dəstəklənmir.")
        };

    private static string NormalizeContentType(string? contentType) =>
        (contentType ?? string.Empty)
            .Split(';', 2)[0]
            .Trim()
            .ToLowerInvariant();

    private static long ToBytes(int sizeInMb) =>
        checked(sizeInMb * 1024L * 1024L);

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            File.Delete(path);
    }

    private static DetectedMedia DetectMedia(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 &&
            header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
            return new DetectedMedia("image/jpeg", ".jpg");

        if (header.Length >= 8 &&
            header[..8].SequenceEqual(new byte[]
                { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return new DetectedMedia("image/png", ".png");

        if (header.Length >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) &&
            header.Slice(8, 4).SequenceEqual("WEBP"u8))
            return new DetectedMedia("image/webp", ".webp");

        if (header.Length >= 12 && header.Slice(4, 4).SequenceEqual("ftyp"u8))
            return new DetectedMedia("video/mp4", ".mp4");

        if (header.Length >= 4 &&
            header[..4].SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }))
            return new DetectedMedia("video/webm", ".webm");

        return new DetectedMedia(null, string.Empty);
    }

    private sealed record QuarantinedFile(
        string Path,
        byte[] Header,
        long Length,
        string OriginalFileName);

    private sealed record DetectedMedia(string? ContentType, string Extension);
}
