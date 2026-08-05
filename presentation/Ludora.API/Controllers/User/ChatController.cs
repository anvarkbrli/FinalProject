using System.Security.Claims;
using Ludora.API.Hubs;
using Ludora.Application.DTOs;
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
        [FromQuery] bool archived = false,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _chatService.GetConversationsAsync(
            GetCurrentUserId(),
            archived,
            search,
            cancellationToken));
    }

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<ChatConversationDTO>>> GetRequests(
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _chatService.GetMessageRequestsAsync(
            GetCurrentUserId(),
            search,
            cancellationToken));
    }

    [HttpPost("groups")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 6L * 1024 * 1024, ValueCountLimit = int.MaxValue)]
    [RequestSizeLimit(6L * 1024 * 1024)]
    public async Task<ActionResult<ChatGroupDTO>> CreateGroup(
        [FromForm] ChatGroupCreateFormRequest request,
        CancellationToken cancellationToken)
    {
        var image = request.Image;
        await using var imageStream = image?.OpenReadStream();
        var userId = GetCurrentUserId();
        var group = await _chatService.CreateGroupAsync(
            userId,
            new ChatGroupCreateDTO
            {
                Name = request.Name,
                Description = request.Description,
                MemberUserIds = request.MemberUserIds,
                Image = image is null || imageStream is null
                    ? null
                    : new ProfileImageUploadDTO
                    {
                        Stream = imageStream,
                        FileName = image.FileName,
                        ContentType = image.ContentType,
                        Length = image.Length
                    }
            },
            cancellationToken);

        var participantIds = group.Members.Select(member => member.Id).Distinct().ToList();
        var changed = new ChatGroupChangedDTO
        {
            ConversationId = group.ConversationId,
            ChangeType = "Created",
            ParticipantUserIds = participantIds,
            Group = group
        };
        await _hub.Clients.Groups(participantIds.Select(ChatHub.UserGroup).ToArray())
            .SendAsync("ChatGroupChanged", changed, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, group);
    }

    [HttpGet("groups/{conversationId:long}")]
    public async Task<ActionResult<ChatGroupDTO>> GetGroup(
        long conversationId,
        CancellationToken cancellationToken)
    {
        return Ok(await _chatService.GetGroupAsync(
            GetCurrentUserId(),
            conversationId,
            cancellationToken));
    }

    [HttpPut("groups/{conversationId:long}")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 6L * 1024 * 1024, ValueCountLimit = int.MaxValue)]
    [RequestSizeLimit(6L * 1024 * 1024)]
    public async Task<ActionResult<ChatGroupDTO>> UpdateGroup(
        long conversationId,
        [FromForm] ChatGroupUpdateFormRequest request,
        CancellationToken cancellationToken)
    {
        var image = request.Image;
        await using var imageStream = image?.OpenReadStream();
        var userId = GetCurrentUserId();
        var group = await _chatService.UpdateGroupAsync(
            userId,
            conversationId,
            new ChatGroupUpdateDTO
            {
                Name = request.Name,
                Description = request.Description,
                RemoveImage = request.RemoveImage,
                MemberUserIdsToAdd = request.MemberUserIdsToAdd,
                Image = image is null || imageStream is null
                    ? null
                    : new ProfileImageUploadDTO
                    {
                        Stream = imageStream,
                        FileName = image.FileName,
                        ContentType = image.ContentType,
                        Length = image.Length
                    }
            },
            cancellationToken);

        var participantIds = group.Members.Select(member => member.Id).Distinct().ToList();
        await _hub.Clients.Groups(participantIds.Select(ChatHub.UserGroup).ToArray())
            .SendAsync("ChatGroupChanged", new ChatGroupChangedDTO
            {
                ConversationId = conversationId,
                ChangeType = "Updated",
                ParticipantUserIds = participantIds,
                Group = group
            }, cancellationToken);

        return Ok(group);
    }

    [HttpPost("groups/{conversationId:long}/members")]
    public async Task<ActionResult<ChatGroupChangedDTO>> AddGroupMembers(
        long conversationId,
        [FromBody] ChatGroupMembersUpdateDTO request,
        CancellationToken cancellationToken)
    {
        var result = await _chatService.AddGroupMembersAsync(
            GetCurrentUserId(),
            conversationId,
            request.UserIds,
            cancellationToken);
        await _hub.Clients.Groups(result.ParticipantUserIds.Select(ChatHub.UserGroup).ToArray())
            .SendAsync("ChatGroupChanged", result, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("groups/{conversationId:long}/members/{memberUserId:guid}")]
    public async Task<ActionResult<ChatGroupChangedDTO>> RemoveGroupMember(
        long conversationId,
        Guid memberUserId,
        CancellationToken cancellationToken)
    {
        var result = await _chatService.RemoveGroupMemberAsync(
            GetCurrentUserId(),
            conversationId,
            memberUserId,
            cancellationToken);
        await _hub.Clients.Groups(result.ParticipantUserIds.Select(ChatHub.UserGroup).ToArray())
            .SendAsync("ChatGroupChanged", result, cancellationToken);
        return Ok(result);
    }

    [HttpPost("groups/{conversationId:long}/leave")]
    public async Task<ActionResult<ChatGroupChangedDTO>> LeaveGroup(
        long conversationId,
        CancellationToken cancellationToken)
    {
        var result = await _chatService.LeaveGroupAsync(
            GetCurrentUserId(),
            conversationId,
            cancellationToken);

        await _hub.Clients.Groups(result.ParticipantUserIds.Select(ChatHub.UserGroup).ToArray())
            .SendAsync("ChatGroupChanged", result, cancellationToken);
        return Ok(result);
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

    [HttpGet("conversations/{conversationId:long}/messages/{messageId:long}")]
    public async Task<ActionResult<ChatMessageDTO>> GetMessage(
        long conversationId,
        long messageId,
        CancellationToken cancellationToken)
    {
        return Ok(await _chatService.GetMessageAsync(
            GetCurrentUserId(),
            conversationId,
            messageId,
            cancellationToken));
    }

    [HttpGet("conversations/{conversationId:long}/messages/search")]
    public async Task<ActionResult<ChatMessageSearchPageDTO>> SearchMessages(
        long conversationId,
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _chatService.SearchMessagesAsync(
            GetCurrentUserId(),
            conversationId,
            query,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpPost("messages")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 170L * 1024 * 1024)]
    [RequestSizeLimit(170L * 1024 * 1024)]
    public async Task<ActionResult<ChatSendMessageResultDTO>> SendMessage(
        [FromForm] ChatSendMessageFormRequest request,
        CancellationToken cancellationToken)
    {
        var mediaStreams = new List<Stream>();
        try
        {
            var uploads = new List<ChatMediaUploadDTO>();
            foreach (var file in request.Media.Where(file => file.Length > 0))
            {
                var stream = file.OpenReadStream();
                mediaStreams.Add(stream);
                uploads.Add(new ChatMediaUploadDTO
                {
                    Stream = stream,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Length = file.Length
                });
            }

            var senderUserId = GetCurrentUserId();
            var result = await _chatService.SendMessageAsync(
                senderUserId,
                new ChatSendMessageDTO
                {
                    RecipientUserId = request.RecipientUserId,
                    ConversationId = request.ConversationId,
                    Text = request.Text,
                    Media = uploads,
                    GameSource = request.GameSource,
                    GameId = request.GameId,
                    GameName = request.GameName,
                    GameCoverUrl = request.GameCoverUrl,
                    ReplyToMessageId = request.ReplyToMessageId
                },
                cancellationToken);

            var participants = await _chatService.GetParticipantsAsync(
                senderUserId,
                result.Message.ConversationId,
                cancellationToken);
            await BroadcastMessageCreatedAsync(
                participants.UserIds,
                result.Message.ConversationId,
                result.Message.Id,
                cancellationToken);

            if (result.StreakUpdate is not null)
            {
                await _hub.Clients.Groups(
                        participants.UserIds.Select(ChatHub.UserGroup).ToArray())
                    .SendAsync("ChatStreakUpdated", result.StreakUpdate, cancellationToken);
            }

            return StatusCode(StatusCodes.Status201Created, result);
        }
        finally
        {
            foreach (var stream in mediaStreams)
                await stream.DisposeAsync();
        }
    }

    [HttpPost("messages/{messageId:long}/forward")]
    public async Task<ActionResult<ChatForwardMessagesResultDTO>> ForwardMessage(
        long messageId,
        [FromBody] ChatForwardMessageRequestDTO request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.ForwardMessageAsync(
            userId,
            messageId,
            request.TargetConversationIds,
            cancellationToken);

        foreach (var item in result.Items)
        {
            var participantIds = item.RecipientUserIds
                .Append(userId)
                .Distinct()
                .ToList();
            await BroadcastMessageCreatedAsync(
                participantIds,
                item.ConversationId,
                item.Message.Id,
                cancellationToken);

            if (item.StreakUpdate is not null)
            {
                await _hub.Clients.Groups(participantIds.Select(ChatHub.UserGroup).ToArray())
                    .SendAsync("ChatStreakUpdated", item.StreakUpdate, cancellationToken);
            }
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("requests/{conversationId:long}/accept")]
    public async Task<ActionResult<ChatRequestDecisionDTO>> Accept(
        long conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var participants = await _chatService.GetParticipantsAsync(
            userId,
            conversationId,
            cancellationToken);
        var result = await _chatService.AcceptRequestAsync(
            userId,
            conversationId,
            cancellationToken);

        await BroadcastDecisionAsync(
            result,
            participants.UserIds,
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("requests/{conversationId:long}/reject")]
    public async Task<ActionResult<ChatRequestDecisionDTO>> Reject(
        long conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var participants = await _chatService.GetParticipantsAsync(
            userId,
            conversationId,
            cancellationToken);
        var result = await _chatService.RejectRequestAsync(
            userId,
            conversationId,
            cancellationToken);

        await BroadcastDecisionAsync(
            result,
            participants.UserIds,
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("conversations/{conversationId:long}/read")]
    public async Task<ActionResult<ChatReadReceiptDTO>> MarkRead(
        long conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var participants = await _chatService.GetParticipantsAsync(
            userId,
            conversationId,
            cancellationToken);
        var receipt = await _chatService.MarkReadAsync(
            userId,
            conversationId,
            cancellationToken);

        await _hub.Clients.Groups(participants.UserIds.Select(ChatHub.UserGroup).ToArray())
            .SendAsync("MessagesRead", receipt, cancellationToken);
        return Ok(receipt);
    }

    [HttpPost("conversations/{conversationId:long}/archive")]
    public async Task<ActionResult<ChatConversationArchiveChangedDTO>> ArchiveConversation(
        long conversationId,
        CancellationToken cancellationToken)
    {
        return Ok(await ChangeArchiveStateAsync(
            conversationId,
            isArchived: true,
            cancellationToken));
    }

    [HttpDelete("conversations/{conversationId:long}/archive")]
    public async Task<ActionResult<ChatConversationArchiveChangedDTO>> UnarchiveConversation(
        long conversationId,
        CancellationToken cancellationToken)
    {
        return Ok(await ChangeArchiveStateAsync(
            conversationId,
            isArchived: false,
            cancellationToken));
    }

    [HttpDelete("conversations/{conversationId:long}")]
    public async Task<ActionResult<ChatConversationDeletedDTO>> DeleteConversation(
        long conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.DeleteConversationForUserAsync(
            userId,
            conversationId,
            cancellationToken);

        await _hub.Clients.Group(ChatHub.UserGroup(userId))
            .SendAsync("ConversationDeleted", result, cancellationToken);

        return Ok(result);
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

    [HttpPut("messages/{messageId:long}/reaction")]
    public async Task<ActionResult<ChatMessageReactionChangedDTO>> SetReaction(
        long messageId,
        [FromBody] SetChatReactionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await ChangeReactionAsync(
            messageId,
            request.ReactionType,
            cancellationToken));
    }

    [HttpDelete("messages/{messageId:long}/reaction")]
    public async Task<ActionResult<ChatMessageReactionChangedDTO>> RemoveReaction(
        long messageId,
        CancellationToken cancellationToken)
    {
        return Ok(await ChangeReactionAsync(
            messageId,
            reactionType: null,
            cancellationToken));
    }

    [HttpPost("messages/bulk-delete")]
    public async Task<ActionResult<ChatBulkDeleteResultDTO>> DeleteMessages(
        [FromBody] ChatBulkDeleteRequestDTO request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.DeleteMessagesAsync(
            userId,
            request.MessageIds,
            request.Scope,
            cancellationToken);
        var participants = await _chatService.GetParticipantsAsync(
            userId,
            result.ConversationId,
            cancellationToken);

        foreach (var item in result.Items)
        {
            var recipients = result.Scope == ChatMessageDeleteScope.ForEveryone
                ? participants.UserIds
                : new[] { userId };
            await _hub.Clients.Groups(recipients.Select(ChatHub.UserGroup).ToArray())
                .SendAsync("MessageDeleted", item, cancellationToken);
        }

        return Ok(result);
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
            var participants = await _chatService.GetParticipantsAsync(
                userId,
                result.ConversationId,
                cancellationToken);
            await _hub.Clients.Groups(participants.UserIds.Select(ChatHub.UserGroup).ToArray())
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
            cancellationToken));
    }

    [HttpDelete("messages/{messageId:long}/pin")]
    public async Task<ActionResult<ChatMessagePinChangedDTO>> UnpinMessage(
        long messageId,
        CancellationToken cancellationToken)
    {
        return Ok(await ChangeMessagePinAsync(
            messageId,
            isPinned: false,
            cancellationToken));
    }

    [HttpGet("messages/{messageId:long}/media")]
    public async Task<IActionResult> GetMedia(
        long messageId,
        CancellationToken cancellationToken)
    {
        var media = await _chatService.OpenMediaAsync(
            GetCurrentUserId(),
            messageId,
            mediaId: null,
            cancellationToken: cancellationToken);
        return File(
            media.Content,
            media.ContentType,
            enableRangeProcessing: true);
    }

    [HttpGet("messages/{messageId:long}/media/{mediaId:long}")]
    public async Task<IActionResult> GetMediaItem(
        long messageId,
        long mediaId,
        CancellationToken cancellationToken)
    {
        var media = await _chatService.OpenMediaAsync(
            GetCurrentUserId(),
            messageId,
            mediaId,
            cancellationToken);
        return File(
            media.Content,
            media.ContentType,
            enableRangeProcessing: true);
    }

    private async Task<ChatConversationArchiveChangedDTO> ChangeArchiveStateAsync(
        long conversationId,
        bool isArchived,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.SetConversationArchivedAsync(
            userId,
            conversationId,
            isArchived,
            cancellationToken);

        await _hub.Clients.Group(ChatHub.UserGroup(userId))
            .SendAsync("ConversationArchiveChanged", result, cancellationToken);
        return result;
    }

    private async Task<ChatMessageReactionChangedDTO> ChangeReactionAsync(
        long messageId,
        ChatReactionType? reactionType,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.SetMessageReactionAsync(
            userId,
            messageId,
            reactionType,
            cancellationToken);
        var participants = await _chatService.GetParticipantsAsync(
            userId,
            result.ConversationId,
            cancellationToken);

        await _hub.Clients.Groups(participants.UserIds.Select(ChatHub.UserGroup).ToArray())
            .SendAsync("MessageReactionChanged", result, cancellationToken);
        return result;
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
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        _hub.Clients.Groups(userIds.Select(ChatHub.UserGroup).ToArray())
            .SendAsync("MessageRequestUpdated", decision, cancellationToken);

    private async Task BroadcastMessageCreatedAsync(
        IReadOnlyCollection<Guid> participantUserIds,
        long conversationId,
        long messageId,
        CancellationToken cancellationToken)
    {
        foreach (var participantUserId in participantUserIds.Distinct())
        {
            var message = await _chatService.GetMessageAsync(
                participantUserId,
                conversationId,
                messageId,
                cancellationToken);
            await _hub.Clients.Group(ChatHub.UserGroup(participantUserId))
                .SendAsync("MessageCreated", message, cancellationToken);
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");
        return userId;
    }

    public sealed record SetChatReactionRequest(ChatReactionType ReactionType);
}

public sealed class ChatGroupCreateFormRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Guid> MemberUserIds { get; set; } = [];
    public IFormFile? Image { get; set; }
}

public sealed class ChatGroupUpdateFormRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RemoveImage { get; set; }
    public List<Guid> MemberUserIdsToAdd { get; set; } = [];
    public IFormFile? Image { get; set; }
}

public sealed class ChatSendMessageFormRequest
{
    public Guid? RecipientUserId { get; set; }
    public long? ConversationId { get; set; }
    public string? Text { get; set; }
    public CatalogSource? GameSource { get; set; }
    public long? GameId { get; set; }
    public string? GameName { get; set; }
    public string? GameCoverUrl { get; set; }
    public long? ReplyToMessageId { get; set; }
    public List<IFormFile> Media { get; set; } = [];
}
