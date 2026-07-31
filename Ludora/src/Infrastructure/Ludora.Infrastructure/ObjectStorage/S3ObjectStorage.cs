using Amazon.S3;
using Amazon.S3.Model;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Application.Settings;

namespace Ludora.Infrastructure.ObjectStorage;

public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;
    private readonly ObjectStorageSettings _settings;

    public S3ObjectStorage(IAmazonS3 client, ObjectStorageSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = BuildObjectKey(key, isPublic),
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<StoredObject> OpenReadAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = _settings.BucketName,
                    Key = BuildObjectKey(key, isPublic)
                },
                cancellationToken);

            return new StoredObject
            {
                Content = new S3ResponseStream(response, response.ContentLength),
                ContentType = string.IsNullOrWhiteSpace(response.Headers.ContentType)
                    ? "application/octet-stream"
                    : response.Headers.ContentType,
                FileName = Path.GetFileName(key),
                Length = response.ContentLength
            };
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Fayl tapılmadı.");
        }
    }

    public async Task<byte[]> ReadAllBytesAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        await using var storedObject = await OpenReadAsync(key, isPublic, cancellationToken);
        using var memory = new MemoryStream();
        await storedObject.Content.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    public async Task DeleteAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = BuildObjectKey(key, isPublic)
            },
            cancellationToken);
    }

    public string GetPublicUrl(string key)
    {
        var objectKey = BuildObjectKey(key, true);
        return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{objectKey}";
    }

    public bool TryGetPublicKey(string urlOrPath, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(urlOrPath))
            return false;

        var baseUrl = _settings.PublicBaseUrl.TrimEnd('/');
        var value = urlOrPath.Trim();
        if (!value.StartsWith(baseUrl + "/", StringComparison.OrdinalIgnoreCase))
            return false;

        var objectKey = value[(baseUrl.Length + 1)..];
        var prefix = NormalizePrefix(_settings.PublicPrefix) + "/";
        if (!objectKey.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        key = NormalizeKey(objectKey[prefix.Length..]);
        return key.Length > 0;
    }

    private string BuildObjectKey(string key, bool isPublic)
    {
        var prefix = NormalizePrefix(isPublic
            ? _settings.PublicPrefix
            : _settings.PrivatePrefix);
        return $"{prefix}/{NormalizeKey(key)}";
    }

    private static string NormalizePrefix(string value) =>
        value.Replace('\\', '/').Trim().Trim('/');

    private static string NormalizeKey(string value)
    {
        var normalized = value.Replace('\\', '/').Trim().Trim('/');
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

    private sealed class S3ResponseStream : Stream
    {
        private readonly GetObjectResponse _response;
        private readonly Stream _inner;
        private readonly long _length;
        private bool _disposed;

        public S3ResponseStream(GetObjectResponse response, long length)
        {
            _response = response;
            _inner = response.ResponseStream;
            _length = length;
        }

        public override bool CanRead => !_disposed && _inner.CanRead;
        public override bool CanSeek => !_disposed && _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _inner.CanSeek ? _inner.Position : throw new NotSupportedException();
            set
            {
                if (!_inner.CanSeek)
                    throw new NotSupportedException();
                _inner.Position = value;
            }
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _response.Dispose();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
