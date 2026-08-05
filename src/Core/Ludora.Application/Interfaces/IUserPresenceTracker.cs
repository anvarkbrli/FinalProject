namespace Ludora.Application.Interfaces
{
    public interface IUserPresenceTracker
    {
        Task<bool> ConnectAsync(
            Guid userId,
            string connectionId,
            CancellationToken cancellationToken = default);

        Task<bool> DisconnectAsync(
            Guid userId,
            string connectionId,
            CancellationToken cancellationToken = default);

        Task<bool> IsOnlineAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlySet<Guid>> GetOnlineUserIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default);

        bool IsOnline(Guid userId)
        {
            return IsOnlineAsync(userId, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
    }
}

