namespace Ludora.Application.Interfaces;

public interface IUserPresenceTracker
{
    bool Connect(Guid userId, string connectionId);
    bool Disconnect(Guid userId, string connectionId);
    bool IsOnline(Guid userId);
}
