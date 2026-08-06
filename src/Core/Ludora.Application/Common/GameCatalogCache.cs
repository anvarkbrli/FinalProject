using Ludora.Application.Interfaces;

namespace Ludora.Application.Common
{
    public static class GameCatalogCache
    {
        public const string VersionKey = "game-catalog:version";

        public static async Task<long> GetVersionAsync(
            IAppCache cache,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(cache);
            var version = await cache.GetAsync<long>(VersionKey, cancellationToken);
            return version > 0 ? version : 1;
        }

        public static Task InvalidateAsync(
            IAppCache cache,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(cache);

            return cache.SetAsync(
                VersionKey,
                DateTime.UtcNow.Ticks,
                TimeSpan.FromDays(365),
                cancellationToken);
        }
    }
}
