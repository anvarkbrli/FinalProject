using System.Security.Claims;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ludora.API.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            UserGroup(userId));

        var conversations = await _chatService.GetConversationsAsync(
            userId,
            Context.ConnectionAborted);
        var requests = await _chatService.GetMessageRequestsAsync(
            userId,
            Context.ConnectionAborted);

        foreach (var conversationId in conversations
                     .Concat(requests)
                     .Select(x => x.Id)
                     .Distinct())
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
            var conversation = conversations
                .Concat(requests)
                .FirstOrDefault(x => x.Id == receipt.ConversationId);
            var groups = new List<string>
            {
                ConversationGroup(receipt.ConversationId),
                UserGroup(userId)
            };
            if (conversation is not null)
                groups.Add(UserGroup(conversation.OtherUser.Id));

            await Clients.Groups(groups)
                .SendAsync("MessageDeliveryChanged", receipt);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(long conversationId)
    {
        await _chatService.GetMessagesAsync(
            GetUserId(),
            conversationId,
            null,
            1,
            Context.ConnectionAborted);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            ConversationGroup(conversationId));
    }

    public async Task MarkRead(long conversationId)
    {
        var userId = GetUserId();
        var conversation = (await _chatService.GetConversationsAsync(
                userId,
                Context.ConnectionAborted))
            .FirstOrDefault(x => x.Id == conversationId);
        var receipt = await _chatService.MarkReadAsync(
            userId,
            conversationId,
            Context.ConnectionAborted);
        var groups = new List<string>
        {
            ConversationGroup(conversationId),
            UserGroup(userId)
        };
        if (conversation is not null)
            groups.Add(UserGroup(conversation.OtherUser.Id));

        await Clients.Groups(groups)
            .SendAsync("MessagesRead", receipt);
    }

    public static string UserGroup(Guid userId) => $"chat:user:{userId:N}";
    public static string ConversationGroup(long conversationId) =>
        $"chat:conversation:{conversationId}";

    private Guid GetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new HubException("İstifadəçi müəyyən edilmədi.");
        return userId;
    }
}
