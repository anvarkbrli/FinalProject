using System.Security.Claims;
using Ludora.API.Hubs;
using Ludora.Application.DTOs.Chat;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace Ludora.API.Controllers.User;

[ApiController]
[Authorize]
[EnableRateLimiting("Chat")]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _hub;

    public ChatController(
        IChatService chatService,
        IHubContext<ChatHub> hub)
    {
        _chatService = chatService;
        _hub = hub;
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ChatConversationDTO>>> GetConversations(
        CancellationToken cancellationToken)
    {
        return Ok(await _chatService.GetConversationsAsync(
            GetCurrentUserId(),
            cancellationToken));
    }

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<ChatConversationDTO>>> GetRequests(
        CancellationToken cancellationToken)
    {
        return Ok(await _chatService.GetMessageRequestsAsync(
            GetCurrentUserId(),
            cancellationToken));
    }

    [HttpGet("conversations/{conversationId:long}/messages")]
    public async Task<ActionResult<ChatMessagePageDTO>> GetMessages(
        long conversationId,
        [FromQuery] long? beforeMessageId,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _chatService.GetMessagesAsync(
            GetCurrentUserId(),
            conversationId,
            beforeMessageId,
            pageSize,
            cancellationToken));
    }

    [HttpPost("messages")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ChatMessageDTO>> SendMessage(
        [FromForm] Guid recipientUserId,
        [FromForm] string? text,
        [FromForm] CatalogSource? gameSource,
        [FromForm] long? gameId,
        [FromForm] string? gameName,
        [FromForm] string? gameCoverUrl,
        IFormFile? media,
        CancellationToken cancellationToken)
    {
        await using var mediaStream = media is { Length: > 0 }
            ? media.OpenReadStream()
            : null;

        var senderUserId = GetCurrentUserId();
        var message = await _chatService.SendMessageAsync(
            senderUserId,
            new ChatSendMessageDTO
            {
                RecipientUserId = recipientUserId,
                Text = text,
                Media = media is null || mediaStream is null
                    ? null
                    : new ChatMediaUploadDTO
                    {
                        Stream = mediaStream,
                        FileName = media.FileName,
                        ContentType = media.ContentType,
                        Length = media.Length
                    },
                GameSource = gameSource,
                GameId = gameId,
                GameName = gameName,
                GameCoverUrl = gameCoverUrl
            },
            cancellationToken);

        await Task.WhenAll(
            _hub.Clients.Group(ChatHub.UserGroup(senderUserId))
                .SendAsync("MessageCreated", message, cancellationToken),
            _hub.Clients.Group(ChatHub.UserGroup(recipientUserId))
                .SendAsync("MessageCreated", message, cancellationToken));

        return StatusCode(StatusCodes.Status201Created, message);
    }

    [HttpPost("requests/{conversationId:long}/accept")]
    public async Task<ActionResult<ChatRequestDecisionDTO>> Accept(
        long conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var requests = await _chatService.GetMessageRequestsAsync(userId, cancellationToken);
        var request = requests.FirstOrDefault(x => x.Id == conversationId);
        var result = await _chatService.AcceptRequestAsync(
            userId,
            conversationId,
            cancellationToken);

        await BroadcastDecisionAsync(result, userId, request?.OtherUser.Id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("requests/{conversationId:long}/reject")]
    public async Task<ActionResult<ChatRequestDecisionDTO>> Reject(
        long conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var requests = await _chatService.GetMessageRequestsAsync(userId, cancellationToken);
        var request = requests.FirstOrDefault(x => x.Id == conversationId);
        var result = await _chatService.RejectRequestAsync(
            userId,
            conversationId,
            cancellationToken);

        await BroadcastDecisionAsync(result, userId, request?.OtherUser.Id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("conversations/{conversationId:long}/read")]
    public async Task<ActionResult<ChatReadReceiptDTO>> MarkRead(
        long conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var conversations = await _chatService.GetConversationsAsync(
            userId,
            cancellationToken);
        var otherUserId = conversations
            .FirstOrDefault(x => x.Id == conversationId)?
            .OtherUser.Id;
        var receipt = await _chatService.MarkReadAsync(
            userId,
            conversationId,
            cancellationToken);
        var groups = new List<string>
        {
            ChatHub.ConversationGroup(conversationId),
            ChatHub.UserGroup(userId)
        };
        if (otherUserId.HasValue)
            groups.Add(ChatHub.UserGroup(otherUserId.Value));

        await _hub.Clients.Groups(groups)
            .SendAsync("MessagesRead", receipt, cancellationToken);
        return Ok(receipt);
    }

    [HttpGet("conversations/{conversationId:long}/pinned-messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDTO>>> GetPinnedMessages(
        long conversationId,
        CancellationToken cancellationToken)
    {
        return Ok(await _chatService.GetPinnedMessagesAsync(
            GetCurrentUserId(),
            conversationId,
            cancellationToken));
    }

    [HttpDelete("messages/{messageId:long}")]
    public async Task<ActionResult<ChatMessageDeletedDTO>> DeleteMessage(
        long messageId,
        [FromQuery] ChatMessageDeleteScope scope = ChatMessageDeleteScope.ForMe,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.DeleteMessageAsync(
            userId,
            messageId,
            scope,
            cancellationToken);

        if (scope == ChatMessageDeleteScope.ForEveryone)
        {
            var conversation = (await _chatService.GetConversationsAsync(
                    userId,
                    cancellationToken))
                .FirstOrDefault(x => x.Id == result.ConversationId);
            var groups = new List<string>
            {
                ChatHub.ConversationGroup(result.ConversationId),
                ChatHub.UserGroup(userId)
            };
            if (conversation is not null)
                groups.Add(ChatHub.UserGroup(conversation.OtherUser.Id));

            await _hub.Clients.Groups(groups.Distinct().ToList())
                .SendAsync("MessageDeleted", result, cancellationToken);
        }
        else
        {
            await _hub.Clients.Group(ChatHub.UserGroup(userId))
                .SendAsync("MessageDeleted", result, cancellationToken);
        }

        return Ok(result);
    }

    [HttpPost("messages/{messageId:long}/pin")]
    public async Task<ActionResult<ChatMessagePinChangedDTO>> PinMessage(
        long messageId,
        CancellationToken cancellationToken)
    {
        return Ok(await ChangeMessagePinAsync(
            messageId,
            isPinned: true,
            cancellationToken: cancellationToken));
    }

    [HttpDelete("messages/{messageId:long}/pin")]
    public async Task<ActionResult<ChatMessagePinChangedDTO>> UnpinMessage(
        long messageId,
        CancellationToken cancellationToken)
    {
        return Ok(await ChangeMessagePinAsync(
            messageId,
            isPinned: false,
            cancellationToken: cancellationToken));
    }

    [HttpGet("messages/{messageId:long}/media")]
    public async Task<IActionResult> GetMedia(
        long messageId,
        CancellationToken cancellationToken)
    {
        var media = await _chatService.OpenMediaAsync(
            GetCurrentUserId(),
            messageId,
            cancellationToken);
        return File(
            media.Content,
            media.ContentType,
            enableRangeProcessing: true);
    }

    private async Task<ChatMessagePinChangedDTO> ChangeMessagePinAsync(
        long messageId,
        bool isPinned,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.SetMessagePinAsync(
            userId,
            messageId,
            isPinned,
            cancellationToken);

        await _hub.Clients.Group(ChatHub.UserGroup(userId))
            .SendAsync("MessagePinChanged", result, cancellationToken);
        return result;
    }

    private Task BroadcastDecisionAsync(
        ChatRequestDecisionDTO decision,
        Guid currentUserId,
        Guid? otherUserId,
        CancellationToken cancellationToken)
    {
        var groups = new List<string> { ChatHub.UserGroup(currentUserId) };
        if (otherUserId.HasValue)
            groups.Add(ChatHub.UserGroup(otherUserId.Value));

        return _hub.Clients.Groups(groups)
            .SendAsync("MessageRequestUpdated", decision, cancellationToken);
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");
        return userId;
    }
}
