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
    [RequestSizeLimit(110 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 110 * 1024 * 1024)]
    public async Task<ActionResult<UserReportDTO>> Create(
        Guid reportedUserId,
        [FromForm] Ludora.Domain.Enums.UserReportCategory category,
        [FromForm] string? description,
        [FromForm] Ludora.Domain.Enums.UserReportContentType reportedContentType,
        [FromForm] long? reportedContentId,
        [FromForm] List<IFormFile>? evidenceFiles,
        [FromForm] List<IFormFile>? evidenceImages,
        IFormFile? evidenceImage,
        CancellationToken cancellationToken)
    {
        var allFiles = (evidenceFiles ?? [])
            .Concat(evidenceImages ?? [])
            .Concat(evidenceImage is { Length: > 0 } ? [evidenceImage] : [])
            .Where(x => x.Length > 0)
            .Take(6)
            .ToList();

        if (allFiles.Sum(x => x.Length) > 100L * 1024 * 1024)
        {
            return BadRequest(new
            {
                message = "Sübut fayllarının ümumi ölçüsü maksimum 100 MB ola bilər."
            });
        }

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
        IFormFile? file,
        Stream? fileStream)
    {
        if (file is null || fileStream is null || file.Length <= 0)
            return null;

        return new UserReportEvidenceUploadDTO
        {
            Stream = fileStream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length
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
