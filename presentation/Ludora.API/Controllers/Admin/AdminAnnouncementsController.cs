using Ludora.Application.Extensions;
using Ludora.API.Security;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Admin;
using Ludora.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.Admin
{
    [Authorize(Policy = CapabilityNames.ViewAnnouncements)]
    [EnableRateLimiting(RateLimitPolicyNames.Moderation)]
    [Route("api/admin/announcements")]
    [ApiController]
    public sealed class AdminAnnouncementsController : ControllerBase
    {
        private readonly IAnnouncementService _announcementService;

        public AdminAnnouncementsController(IAnnouncementService announcementService)
        {
            _announcementService = announcementService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResultDTO<AnnouncementDTO>>> GetAll(
            [FromQuery] AdminAnnouncementFilterDTO filter,
            CancellationToken cancellationToken)
        {
            return Ok(await _announcementService.GetAllForAdminAsync(
                filter,
                cancellationToken));
        }

        [HttpGet("{announcementId:int}")]
        public async Task<ActionResult<AnnouncementDTO>> GetById(
            int announcementId,
            CancellationToken cancellationToken)
        {
            return Ok(await _announcementService.GetByIdForAdminAsync(
                announcementId,
                cancellationToken));
        }

        [HttpPost]
        [Authorize(Policy = CapabilityNames.ManageAnnouncements)]
        public async Task<ActionResult<AnnouncementDTO>> Create(
            [FromBody] AnnouncementCreateDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _announcementService.CreateAsync(
                User.GetRequiredUserId(),
                dto,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { announcementId = result.Id },
                result);
        }

        [HttpPut("{announcementId:int}")]
        [Authorize(Policy = CapabilityNames.ManageAnnouncements)]
        public async Task<ActionResult<AnnouncementDTO>> Update(
            int announcementId,
            [FromBody] AnnouncementUpdateDTO dto,
            CancellationToken cancellationToken)
        {
            return Ok(await _announcementService.UpdateAsync(
                announcementId,
                dto,
                cancellationToken));
        }

        [HttpDelete("{announcementId:int}")]
        [Authorize(Policy = CapabilityNames.ManageAnnouncements)]
        public async Task<IActionResult> Delete(
            int announcementId,
            CancellationToken cancellationToken)
        {
            await _announcementService.DeleteAsync(
                announcementId,
                cancellationToken);

            return NoContent();
        }

        [HttpPost("{announcementId:int}/restore")]
        [Authorize(Policy = CapabilityNames.ManageAnnouncements)]
        public async Task<ActionResult<AnnouncementDTO>> Restore(
            int announcementId,
            CancellationToken cancellationToken)
        {
            return Ok(await _announcementService.RestoreAsync(
                announcementId,
                cancellationToken));
        }
    }
}
