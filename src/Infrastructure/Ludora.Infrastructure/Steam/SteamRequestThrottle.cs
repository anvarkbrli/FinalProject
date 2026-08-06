using System.Threading.RateLimiting;

using Ludora.Application.Exceptions;
using Ludora.Application.Settings;

using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Steam
{
    public sealed class SteamRequestThrottle : IDisposable
    {
        private readonly SemaphoreSlim _concurrencyGate;
        private readonly SlidingWindowRateLimiter _rateLimiter;

        public SteamRequestThrottle(IOptions<SteamSettings> options)
        {
            var settings = options.Value;
            _concurrencyGate = new SemaphoreSlim(
                settings.MaxConcurrentRequests,
                settings.MaxConcurrentRequests);

            _rateLimiter = new SlidingWindowRateLimiter(
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = settings.RequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = settings.RequestsPerMinute,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
        }

        public async Task<IDisposable> EnterAsync(
            CancellationToken cancellationToken = default)
        {
            await _concurrencyGate.WaitAsync(cancellationToken);

            try
            {
                var rateLease = await _rateLimiter.AcquireAsync(1, cancellationToken);

                if (!rateLease.IsAcquired)
                {
                    rateLease.Dispose();
                    throw new ExternalServiceException(
                        "Steam API üçün daxili sorğu limiti dolub. Bir qədər sonra yenidən yoxlayın.");
                }

                return new Lease(_concurrencyGate, rateLease);
            }
            catch
            {
                _concurrencyGate.Release();
                throw;
            }
        }

        public void Dispose()
        {
            _rateLimiter.Dispose();
            _concurrencyGate.Dispose();
        }

        private sealed class Lease : IDisposable
        {
            private SemaphoreSlim? _concurrencyGate;
            private RateLimitLease? _rateLease;

            public Lease(
                SemaphoreSlim concurrencyGate,
                RateLimitLease rateLease)
            {
                _concurrencyGate = concurrencyGate;
                _rateLease = rateLease;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _rateLease, null)?.Dispose();
                Interlocked.Exchange(ref _concurrencyGate, null)?.Release();
            }
        }
    }
}
