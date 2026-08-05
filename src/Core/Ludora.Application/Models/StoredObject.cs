namespace Ludora.Application.Models;

public sealed class StoredObject : IAsyncDisposable
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public long? Length { get; init; }

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
