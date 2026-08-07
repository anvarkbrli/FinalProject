using Ludora.Application.DTOs.Chat;
using Ludora.Application.Interfaces;
using Ludora.Persistence.Data;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Ludora.API.Hubs
{
    public sealed class UserPresenceCoordinator
    {
        private readonly IUserPresenceTracker _tracker;
        private readonly IChatService _chatService;
        private readonly AppDbContext _dbContext;
        private readonly IHubContext<ChatHub> _chatHub;

        public UserPresenceCoordinator(
            IUserPresenceTracker tracker,
            IChatService chatService,
            AppDbContext dbContext,
            IHubContext<ChatHub> chatHub)
        {
            _tracker = tracker;
            _chatService = chatService;
            _dbContext = dbContext;
            _chatHub = chatHub;
        }

        public async Task ConnectedAsync(
            Guid userId,
            string connectionId,
            CancellationToken cancellationToken)
        {
            if (!await _tracker.ConnectAsync(userId, connectionId, cancellationToken))
                return;

            await BroadcastAsync(
                new ChatPresenceChangedDTO
                {
                    UserId = userId,
                    IsOnline = true,
                    LastSeenAtUtc = null
                },
                cancellationToken);
        }

        public Task HeartbeatAsync(
            Guid userId,
            string connectionId,
            CancellationToken cancellationToken) =>
            _tracker.HeartbeatAsync(userId, connectionId, cancellationToken);

        public async Task DisconnectedAsync(
            Guid userId,
            Guid? sessionId,
            string connectionId)
        {
            if (!await _tracker.DisconnectAsync(userId, connectionId))
                return;

            var now = DateTime.UtcNow;
            var session = sessionId.HasValue
                ? await _dbContext.RefreshSessions.FirstOrDefaultAsync(item =>
                    item.Id == sessionId.Value && item.UserId == userId)
                : await _dbContext.RefreshSessions
                    .Where(item => item.UserId == userId)
                    .OrderByDescending(item => item.LastActiveAtUtc)
                    .FirstOrDefaultAsync();

            if (session is not null)
            {
                session.LastActiveAtUtc = now;
                await _dbContext.SaveChangesAsync();
            }

            await BroadcastAsync(
                new ChatPresenceChangedDTO
                {
                    UserId = userId,
                    IsOnline = false,
                    LastSeenAtUtc = now
                },
                CancellationToken.None);
        }

        private async Task BroadcastAsync(
            ChatPresenceChangedDTO presence,
            CancellationToken cancellationToken)
        {
            var partnerIds = await _chatService.GetConversationPartnerIdsAsync(
                presence.UserId,
                cancellationToken);
            var groups = partnerIds
                .Append(presence.UserId)
                .Distinct()
                .Select(ChatHub.UserGroup)
                .ToArray();

            if (groups.Length == 0)
                return;

            await _chatHub.Clients.Groups(groups)
                .SendAsync("UserPresenceChanged", presence, cancellationToken);
        }
    }
}
