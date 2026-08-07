using Ludora.API.Configuration;
using Ludora.API.Hubs;

using Microsoft.Extensions.Options;

namespace Ludora.API.Tests
{
    public sealed class UserPresenceTrackerTests
    {
        [Fact]
        public async Task MultipleConnections_StayOnlineUntilLastDisconnect()
        {
            var tracker = CreateTracker();
            var userId = Guid.NewGuid();

            Assert.True(await tracker.ConnectAsync(userId, "chat-1"));
            Assert.False(await tracker.ConnectAsync(userId, "notification-1"));
            Assert.True(await tracker.IsOnlineAsync(userId));

            Assert.False(await tracker.DisconnectAsync(userId, "chat-1"));
            Assert.True(await tracker.IsOnlineAsync(userId));

            Assert.True(await tracker.DisconnectAsync(userId, "notification-1"));
            Assert.False(await tracker.IsOnlineAsync(userId));
        }

        [Fact]
        public async Task BatchLookup_ReturnsOnlyOnlineUsers()
        {
            var tracker = CreateTracker();
            var onlineUserId = Guid.NewGuid();
            var offlineUserId = Guid.NewGuid();
            await tracker.ConnectAsync(onlineUserId, "connection");

            var result = await tracker.GetOnlineUserIdsAsync(
                [onlineUserId, offlineUserId]);

            Assert.Contains(onlineUserId, result);
            Assert.DoesNotContain(offlineUserId, result);
        }

        private static UserPresenceTracker CreateTracker()
        {
            return new UserPresenceTracker(
                Options.Create(new PresenceSettings
                {
                    OfflineAfterSeconds = 90
                }));
        }
    }
}
