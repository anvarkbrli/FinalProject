using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Application.Settings;

namespace Ludora.Infrastructure.ObjectStorage;

public sealed class LocalObjectStorage : IObjectStorage
{
    private readonly FileStorageSettings _fileSettings;

    public LocalObjectStorage(FileStorageSettings fileSettings)
    {
        _fileSettings = fileSettings;
    }

    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(key, isPublic);
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

    public Task<StoredObject> OpenReadAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = GetFullPath(key, isPublic);

        if (!File.Exists(fullPath))
            throw new NotFoundException("Fayl tapılmadı.");

        var info = new FileInfo(fullPath);
        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(new StoredObject
        {
            Content = stream,
            ContentType = GetContentType(fullPath),
            FileName = Path.GetFileName(fullPath),
            Length = info.Length
        });
    }

    public async Task<byte[]> ReadAllBytesAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(key, isPublic);
        if (!File.Exists(fullPath))
            throw new NotFoundException("Fayl tapılmadı.");

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public Task DeleteAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = GetFullPath(key, isPublic);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) =>
        $"/uploads/{NormalizeKey(key)}";

    public bool TryGetPublicKey(string urlOrPath, out string key)
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

        key = NormalizeKey(value[prefix.Length..]);
        return key.Length > 0;
    }

    private string GetFullPath(string key, bool isPublic)
    {
        var root = isPublic
            ? Path.Combine(_fileSettings.WebRootPath, "uploads")
            : _fileSettings.PrivateStoragePath;
        var normalizedRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(
            Path.Combine(normalizedRoot, NormalizeKey(key).Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Fayl yolu düzgün deyil.");

        return fullPath;
    }

    private static string NormalizeKey(string key)
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

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => "application/octet-stream"
        };
}
