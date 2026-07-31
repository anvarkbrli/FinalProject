using Ludora.API.Security;
using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers;

[Route("api/forum-communities")]
[ApiController]
public sealed class ForumCommunitiesController : ControllerBase
{
    private readonly IForumCommunityService _communityService;

    public ForumCommunitiesController(IForumCommunityService communityService)
    {
        _communityService = communityService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ForumCommunitySummaryDTO>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetAllAsync(
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ForumCommunitySummaryDTO>>> GetMine(
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetMineAsync(
            GetCurrentUserId(),
            cancellationToken));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ForumCommunitySummaryDTO>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetBySlugAsync(
            slug,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.Uploads)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ForumCommunitySummaryDTO>> Create(
        [FromForm] string name,
        [FromForm] string description,
        [FromForm] ForumCommunityCategory category,
        [FromForm] ForumCommunityVisibility visibility,
        [FromForm] bool hasIcon,
        [FromForm] bool hasCoverImage,
        [FromForm] List<IFormFile>? imageFiles,
        CancellationToken cancellationToken)
    {
        var files = imageFiles ?? [];
        var icon = hasIcon ? files.ElementAtOrDefault(0) : null;
        var coverIndex = hasIcon ? 1 : 0;
        var coverImage = hasCoverImage
            ? files.ElementAtOrDefault(coverIndex)
            : null;

        var result = await _communityService.CreateAsync(
            GetCurrentUserId(),
            new ForumCommunityCreateDTO
            {
                Name = name,
                Description = description,
                Category = category,
                Visibility = visibility,
                Icon = ToUpload(icon),
                CoverImage = ToUpload(coverImage)
            },
            cancellationToken);

        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpPut("{id:int}/join")]
    public async Task<IActionResult> Join(int id, CancellationToken cancellationToken)
    {
        await _communityService.JoinAsync(id, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpDelete("{id:int}/membership")]
    public async Task<ActionResult<ForumCommunityLeaveResultDTO>> Leave(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.LeaveAsync(
            id,
            GetCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpPut("{id:int}/follow")]
    public async Task<IActionResult> Follow(int id, CancellationToken cancellationToken)
    {
        await _communityService.FollowAsync(id, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpDelete("{id:int}/follow")]
    public async Task<IActionResult> Unfollow(int id, CancellationToken cancellationToken)
    {
        await _communityService.UnfollowAsync(id, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("{id:int}/membership-requests")]
    public async Task<ActionResult<IReadOnlyList<ForumCommunityMemberDTO>>> GetMembershipRequests(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetMembershipRequestsAsync(
            id,
            GetCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpPut("{id:int}/membership-requests/{membershipId:int}/approve")]
    public async Task<IActionResult> ApproveMembership(
        int id,
        int membershipId,
        CancellationToken cancellationToken)
    {
        await _communityService.ApproveMembershipAsync(
            id,
            membershipId,
            GetCurrentUserId(),
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpDelete("{id:int}/membership-requests/{membershipId:int}")]
    public async Task<IActionResult> RejectMembership(
        int id,
        int membershipId,
        CancellationToken cancellationToken)
    {
        await _communityService.RejectMembershipAsync(
            id,
            membershipId,
            GetCurrentUserId(),
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("{id:int}/members")]
    public async Task<ActionResult<IReadOnlyList<ForumCommunityMemberDTO>>> GetMembers(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.GetMembersAsync(
            id,
            GetCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [HttpGet("{id:int}/invite-candidates")]
    public async Task<ActionResult<IReadOnlyList<ForumCommunityInviteCandidateDTO>>> SearchInviteCandidates(
        int id,
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        return Ok(await _communityService.SearchInviteCandidatesAsync(
            id,
            GetCurrentUserId(),
            query,
            cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpDelete("{id:int}/members/{membershipId:int}")]
    public async Task<IActionResult> RemoveMember(
        int id,
        int membershipId,
        CancellationToken cancellationToken)
    {
        await _communityService.RemoveMemberAsync(
            id,
            membershipId,
            GetCurrentUserId(),
            cancellationToken);
        return NoContent();
    }


    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpPut("{id:int}/members/{membershipId:int}/role")]
    public async Task<IActionResult> UpdateMemberRole(
        int id,
        int membershipId,
        [FromBody] ForumCommunityMemberRoleUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        await _communityService.UpdateMemberRoleAsync(
            id,
            membershipId,
            GetCurrentUserId(),
            dto.Role,
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        await _communityService.DeleteAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpPost("{id:int}/invitations")]
    public async Task<IActionResult> Invite(
        int id,
        [FromBody] ForumCommunityInviteDTO dto,
        CancellationToken cancellationToken)
    {
        await _communityService.InviteAsync(
            id,
            GetCurrentUserId(),
            dto.UserName,
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpPut("{id:int}/invitations/accept")]
    public async Task<IActionResult> AcceptInvite(
        int id,
        CancellationToken cancellationToken)
    {
        await _communityService.AcceptInviteAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);
        return NoContent();
    }


    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpPut("{id:int}/ownership-offer/accept")]
    public async Task<IActionResult> AcceptOwnershipOffer(
        int id,
        CancellationToken cancellationToken)
    {
        await _communityService.AcceptOwnershipOfferAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
    [HttpDelete("{id:int}/ownership-offer")]
    public async Task<IActionResult> DeclineOwnershipOffer(
        int id,
        CancellationToken cancellationToken)
    {
        await _communityService.DeclineOwnershipOfferAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);
        return NoContent();
    }

    private static ProfileImageUploadDTO? ToUpload(IFormFile? file)
    {
        return file is null
            ? null
            : new ProfileImageUploadDTO
            {
                Stream = file.OpenReadStream(),
                Length = file.Length,
                ContentType = file.ContentType,
                FileName = file.FileName
            };
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");
        return userId;
    }

    private Guid? GetOptionalCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
