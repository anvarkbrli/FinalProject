using System.Security.Claims;

using Ludora.Application.DTOs.Chat;
using Ludora.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ludora.API.Hubs
{
    [Authorize]
    public sealed class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly UserPresenceCoordinator _presenceCoordinator;

        public ChatHub(
            IChatService chatService,
            UserPresenceCoordinator presenceCoordinator)
        {
            _chatService = chatService;
            _presenceCoordinator = presenceCoordinator;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                UserGroup(userId));
            await _presenceCoordinator.ConnectedAsync(
                userId,
                PresenceConnectionId(),
                Context.ConnectionAborted);

            var conversations = await _chatService.GetConversationsAsync(
                userId,
                archived: false,
                cancellationToken: Context.ConnectionAborted);
            var archivedConversations = await _chatService.GetConversationsAsync(
                userId,
                archived: true,
                cancellationToken: Context.ConnectionAborted);
            var requests = await _chatService.GetMessageRequestsAsync(
                userId,
                cancellationToken: Context.ConnectionAborted);

            var allConversations = conversations
                .Concat(archivedConversations)
                .Concat(requests)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToList();

            foreach (var conversationId in allConversations.Select(x => x.Id))
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    ConversationGroup(conversationId));
            }

            var receipts = await _chatService.MarkDeliveredAsync(
                userId,
                Context.ConnectionAborted);
            foreach (var receipt in receipts)
            {
                await Clients.Group(ConversationGroup(receipt.ConversationId))
                    .SendAsync("MessageDeliveryChanged", receipt);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(value, out var userId))
            {
                await _presenceCoordinator.DisconnectedAsync(
                    userId,
                    GetSessionId(),
                    PresenceConnectionId());
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinConversation(long conversationId)
        {
            await _chatService.GetParticipantsAsync(
                GetUserId(),
                conversationId,
                Context.ConnectionAborted);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                ConversationGroup(conversationId));
        }

        public Task LeaveConversation(long conversationId) =>
            Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                ConversationGroup(conversationId));

        public Task Heartbeat() =>
            _presenceCoordinator.HeartbeatAsync(
                GetUserId(),
                PresenceConnectionId(),
                Context.ConnectionAborted);

        public async Task SetTyping(long conversationId, bool isTyping)
        {
            var now = DateTime.UtcNow;
            const string throttleKey = "chat:typing:last-sent";
            if (isTyping &&
                Context.Items.TryGetValue(throttleKey, out var stored) &&
                stored is DateTime lastSentAtUtc &&
                now - lastSentAtUtc < TimeSpan.FromMilliseconds(350))
            {
                return;
            }
            Context.Items[throttleKey] = now;

            var userId = GetUserId();
            await _chatService.GetParticipantsAsync(
                userId,
                conversationId,
                Context.ConnectionAborted);

            var typing = new ChatTypingDTO
            {
                ConversationId = conversationId,
                UserId = userId,
                IsTyping = isTyping,
                ExpiresAtUtc = now.AddSeconds(isTyping ? 5 : 0)
            };

            await Clients.OthersInGroup(ConversationGroup(conversationId))
                .SendAsync("ChatTyping", typing, Context.ConnectionAborted);
        }

        public async Task MarkRead(long conversationId)
        {
            var userId = GetUserId();
            var participants = await _chatService.GetParticipantsAsync(
                userId,
                conversationId,
                Context.ConnectionAborted);
            var receipt = await _chatService.MarkReadAsync(
                userId,
                conversationId,
                Context.ConnectionAborted);

            await Clients.Groups(participants.UserIds.Select(UserGroup).ToArray())
                .SendAsync("MessagesRead", receipt);
        }

        public static string UserGroup(Guid userId) => $"chat:user:{userId:N}";
        public static string ConversationGroup(long conversationId) =>
            $"chat:conversation:{conversationId}";

        private string PresenceConnectionId() =>
            $"chat:{Context.ConnectionId}";

        private Guid? GetSessionId()
        {
            var value = Context.User?.FindFirstValue("session_id");
            return Guid.TryParse(value, out var sessionId) ? sessionId : null;
        }

        private Guid GetUserId()
        {
            var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(value, out var userId))
                throw new HubException("İstifadəçi müəyyən edilmədi.");
            return userId;
        }
    }
}
