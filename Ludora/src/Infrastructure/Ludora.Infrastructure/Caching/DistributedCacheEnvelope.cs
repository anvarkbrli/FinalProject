namespace Ludora.Infrastructure.Caching;

public sealed class DistributedCacheEnvelope<T>
{
    public DateTime ExpiresAtUtc { get; init; }
    public T? Value { get; init; }
}
