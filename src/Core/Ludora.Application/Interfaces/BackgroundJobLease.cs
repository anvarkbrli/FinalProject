namespace Ludora.Application.Interfaces
{
    public sealed record BackgroundJobLease(
        long JobId,
        Guid UserId,
        int AttemptCount);
}
