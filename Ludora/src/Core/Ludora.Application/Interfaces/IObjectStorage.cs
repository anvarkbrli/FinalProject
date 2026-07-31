using Ludora.Application.Models;

namespace Ludora.Application.Interfaces;

public interface IObjectStorage
{
    Task PutAsync(
        string key,
        Stream content,
        string contentType,
        bool isPublic,
        CancellationToken cancellationToken = default);

    Task<StoredObject> OpenReadAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken = default);

    Task<byte[]> ReadAllBytesAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string key,
        bool isPublic,
        CancellationToken cancellationToken = default);

    string GetPublicUrl(string key);

    bool TryGetPublicKey(string urlOrPath, out string key);
}
