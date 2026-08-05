namespace Ludora.Infrastructure.IGDB;

public sealed class IgdbRequestThrottle
{
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromMilliseconds(260);
    private readonly SemaphoreSlim _openRequestGate = new(8, 8);
    private readonly SemaphoreSlim _scheduleGate = new(1, 1);
    private DateTimeOffset _nextRequestAtUtc = DateTimeOffset.MinValue;

    public async Task<IDisposable> EnterAsync(
        CancellationToken cancellationToken = default)
    {
        await _openRequestGate.WaitAsync(cancellationToken);

        try
        {
            await _scheduleGate.WaitAsync(cancellationToken);

            try
            {
                var delay = _nextRequestAtUtc - DateTimeOffset.UtcNow;

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);

                _nextRequestAtUtc = DateTimeOffset.UtcNow.Add(MinimumRequestInterval);
            }
            finally
            {
                _scheduleGate.Release();
            }

            return new Lease(_openRequestGate);
        }
        catch
        {
            _openRequestGate.Release();
            throw;
        }
    }

    private sealed class Lease : IDisposable
    {
        private SemaphoreSlim? _gate;

        public Lease(SemaphoreSlim gate)
        {
            _gate = gate;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
        }
    }
}
