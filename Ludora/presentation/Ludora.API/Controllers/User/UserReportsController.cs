using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.User;

[Route("api/user-reports")]
[ApiController]
public sealed class UserReportsController : ControllerBase
{
    private readonly IUserReportService _userReportService;

    public UserReportsController(IUserReportService userReportService)
    {
        _userReportService = userReportService;
    }

    [HttpGet("categories")]
    public ActionResult<IReadOnlyList<UserReportCategoryOptionDTO>> GetCategories()
    {
        return Ok(_userReportService.GetCategoryOptions());
    }

    [Authorize]
    [EnableRateLimiting("UserReports")]
    [HttpPost("users/{reportedUserId:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UserReportDTO>> Create(
        Guid reportedUserId,
        [FromForm] Ludora.Domain.Enums.UserReportCategory category,
        [FromForm] string? description,
        IFormFile? evidenceImage,
        CancellationToken cancellationToken)
    {
        await using var evidenceStream = evidenceImage is { Length: > 0 }
            ? evidenceImage.OpenReadStream()
            : null;

        var result = await _userReportService.CreateAsync(
            GetCurrentUserId(),
            reportedUserId,
            new UserReportCreateDTO
            {
                Category = category,
                Description = description,
                EvidenceImage = CreateEvidenceUpload(
                    evidenceImage,
                    evidenceStream)
            },
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    private static UserReportEvidenceUploadDTO? CreateEvidenceUpload(
        IFormFile? image,
        Stream? imageStream)
    {
        if (image is null || imageStream is null || image.Length <= 0)
            return null;

        return new UserReportEvidenceUploadDTO
        {
            Stream = imageStream,
            FileName = image.FileName,
            ContentType = image.ContentType,
            Length = image.Length
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException(
                "İstifadəçi müəyyən edilmədi.");
        }

        return userId;
    }
}
