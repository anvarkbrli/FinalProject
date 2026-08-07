namespace Ludora.Application.Interfaces
{
    public interface ISteamSyncQueue
    {
        Task<bool> EnqueueAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<BackgroundJobLease?> TryDequeueAsync(CancellationToken cancellationToken = default);
        Task CompleteAsync(long jobId, CancellationToken cancellationToken = default);
        Task FailAsync(long jobId, string error, CancellationToken cancellationToken = default);
    }
}
