using Ludora.API.Extensions;
using Ludora.API.Security;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Contact;
using Ludora.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/contact-messages")]
    [Authorize(Policy = CapabilityNames.ViewContactMessages)]
    [EnableRateLimiting(RateLimitPolicyNames.Moderation)]
    public sealed class AdminContactMessagesController : ControllerBase
    {
        private readonly IContactMessageService _contactMessageService;

        public AdminContactMessagesController(IContactMessageService contactMessageService)
        {
            _contactMessageService = contactMessageService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResultDTO<ContactMessageDTO>>> GetAll(
            [FromQuery] AdminContactMessageFilterDTO filter,
            CancellationToken cancellationToken)
        {
            return Ok(await _contactMessageService.GetAllForAdminAsync(
                filter,
                cancellationToken));
        }

        [HttpGet("{contactMessageId:int}")]
        public async Task<ActionResult<ContactMessageDTO>> GetById(
            int contactMessageId,
            CancellationToken cancellationToken)
        {
            return Ok(await _contactMessageService.GetByIdForAdminAsync(
                contactMessageId,
                cancellationToken));
        }

        [HttpPatch("{contactMessageId:int}/status")]
        [Authorize(Policy = CapabilityNames.ManageContactMessages)]
        public async Task<ActionResult<ContactMessageDTO>> UpdateStatus(
            int contactMessageId,
            [FromBody] ContactMessageStatusUpdateDTO dto,
            CancellationToken cancellationToken)
        {
            return Ok(await _contactMessageService.UpdateStatusAsync(
                contactMessageId,
                dto.Status,
                cancellationToken));
        }

        [HttpPost("{contactMessageId:int}/reply")]
        [Authorize(Policy = CapabilityNames.ManageContactMessages)]
        public async Task<ActionResult<ContactMessageDTO>> Reply(
            int contactMessageId,
            [FromBody] ContactMessageReplyDTO dto,
            CancellationToken cancellationToken)
        {
            return Ok(await _contactMessageService.ReplyAsync(
                contactMessageId,
                User.GetRequiredUserId(),
                dto,
                cancellationToken));
        }
    }
}
