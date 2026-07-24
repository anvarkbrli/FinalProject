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
        [FromForm] Ludora.Domain.Enums.UserReportContentType reportedContentType,
        [FromForm] long? reportedContentId,
        [FromForm] List<IFormFile>? evidenceImages,
        IFormFile? evidenceImage,
        CancellationToken cancellationToken)
    {
        var allFiles = (evidenceImages ?? [])
            .Concat(evidenceImage is { Length: > 0 } ? [evidenceImage] : [])
            .Where(x => x.Length > 0)
            .Take(6)
            .ToList();
        var streams = allFiles.Select(x => x.OpenReadStream()).ToList();

        try
        {
            var result = await _userReportService.CreateAsync(
                GetCurrentUserId(),
                reportedUserId,
                new UserReportCreateDTO
                {
                    Category = category,
                    Description = description,
                    ReportedContentType = reportedContentType,
                    ReportedContentId = reportedContentId,
                    EvidenceImages = allFiles
                        .Zip(streams)
                        .Select(pair => CreateEvidenceUpload(
                            pair.First,
                            pair.Second)!)
                        .ToList()
                },
                cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }
        finally
        {
            foreach (var stream in streams)
                await stream.DisposeAsync();
        }
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
