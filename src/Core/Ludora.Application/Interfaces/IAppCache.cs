namespace Ludora.Application.Interfaces
{
    public interface IAppCache
    {
        Task<T?> GetAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        Task SetAsync<T>(
            string key,
            T value,
            TimeSpan absoluteExpiration,
            CancellationToken cancellationToken = default);

        Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan absoluteExpiration,
            CancellationToken cancellationToken = default)
            where T : class;

        Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default);
    }
}
