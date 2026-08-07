using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;

namespace Ludora.API.BackgroundServices
{
    public sealed class SteamSyncQueue : DurableUserJobQueue, ISteamSyncQueue
    {
        public SteamSyncQueue(IServiceScopeFactory scopeFactory, ILogger<SteamSyncQueue> logger)
            : base(BackgroundJobType.SteamSync, maxAttempts: 5, scopeFactory, logger)
        {
        }

        public Task<bool> EnqueueAsync(Guid userId, CancellationToken cancellationToken = default) =>
            EnqueueCoreAsync(userId, cancellationToken);
        public Task<BackgroundJobLease?> TryDequeueAsync(CancellationToken cancellationToken = default) =>
            TryDequeueCoreAsync(cancellationToken);
        public Task CompleteAsync(long jobId, CancellationToken cancellationToken = default) =>
            CompleteCoreAsync(jobId, cancellationToken);
        public Task FailAsync(long jobId, string error, CancellationToken cancellationToken = default) =>
            FailCoreAsync(jobId, error, cancellationToken);
    }
}
