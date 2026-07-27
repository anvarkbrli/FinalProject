namespace Ludora.Application.Interfaces;

public interface IUserPresenceTracker
{
    void Connect(Guid userId, string connectionId);
    void Disconnect(Guid userId, string connectionId);
    bool IsOnline(Guid userId);
}
