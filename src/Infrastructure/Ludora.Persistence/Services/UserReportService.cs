using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class UserReportService : IUserReportService
{
    private const int MaxDescriptionLength = 2000;
    private const int MinDescriptionLength = 10;
    private const int MaxContentSnapshotLength = 2000;
    private const int MaxReportsPer24Hours = 10;
    private const int MaxEvidenceImages = 5;

    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public UserReportService(
        AppDbContext context,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public IReadOnlyList<UserReportCategoryOptionDTO> GetCategoryOptions()
    {
        return
        [
            CreateCategoryOption(UserReportCategory.Spam, "Spam və təkrarlanan məzmun"),
            CreateCategoryOption(UserReportCategory.HarassmentOrInsult, "Təhqir və narahat etmə"),
            CreateCategoryOption(UserReportCategory.HateSpeech, "Nifrət nitqi"),
            CreateCategoryOption(UserReportCategory.InappropriateContent, "Uyğunsuz məzmun"),
            CreateCategoryOption(UserReportCategory.Impersonation, "Başqasını təqlid etmə"),
            CreateCategoryOption(UserReportCategory.ScamOrFraud, "Dələduzluq və saxtakarlıq"),
            CreateCategoryOption(UserReportCategory.ThreatsOrViolence, "Təhdid və zorakılıq"),
            CreateCategoryOption(UserReportCategory.PrivacyViolation, "Məxfilik pozuntusu"),
            CreateCategoryOption(UserReportCategory.Other, "Digər")
        ];
    }

    public async Task<UserReportDTO> CreateAsync(
        Guid reporterUserId,
        Guid reportedUserId,
        UserReportCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        if (reporterUserId == Guid.Empty || reportedUserId == Guid.Empty)
            throw new BadRequestException("İstifadəçi ID-si düzgün deyil.");

        if (reporterUserId == reportedUserId)
            throw new BadRequestException("İstifadəçi öz hesabını şikayət edə bilməz.");

        ArgumentNullException.ThrowIfNull(dto);

        if (!Enum.IsDefined(dto.Category))
            throw new BadRequestException("Şikayət kateqoriyası düzgün deyil.");

        if (!Enum.IsDefined(dto.ReportedContentType))
            throw new BadRequestException("Şikayət edilən məzmun növü düzgün deyil.");

        var description = dto.Description.NormalizeOptionalText(
            "Şikayət açıqlaması",
            MaxDescriptionLength);

        if (description is not null && description.Length < MinDescriptionLength)
        {
            throw new BadRequestException(
                $"Şikayət açıqlaması ən azı {MinDescriptionLength} simvol olmalıdır.");
        }

        var users = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == reporterUserId || x.Id == reportedUserId)
            .Select(x => new
            {
                x.Id,
                UserName = x.UserName ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        if (users.All(x => x.Id != reporterUserId))
            throw new NotFoundException("Şikayət edən istifadəçi tapılmadı.");

        var reportedUser = users.FirstOrDefault(x => x.Id == reportedUserId)
            ?? throw new NotFoundException("Şikayət edilən istifadəçi tapılmadı.");

        var contentReference = await ResolveReportedContentAsync(
            dto.ReportedContentType,
            dto.ReportedContentId,
            reportedUserId,
            reportedUser.UserName,
            cancellationToken);

        if (dto.EvidenceImages.Count > MaxEvidenceImages)
            throw new BadRequestException($"Maksimum {MaxEvidenceImages} sübut faylı əlavə edilə bilər.");

        if (description is null &&
            dto.EvidenceImages.Count == 0 &&
            contentReference.ContentId is null)
        {
            throw new BadRequestException(
                "Sübut kimi ən azı açıqlama, media faylı və ya konkret məzmun əlavə edilməlidir.");
        }

        var hasActiveReport = await _context.UserReports
            .AsNoTracking()
            .AnyAsync(x =>
                    x.ReporterUserId == reporterUserId &&
                    x.ReportedUserId == reportedUserId &&
                    x.ReportedContentType == contentReference.ContentType &&
                    x.ReportedContentId == contentReference.ContentId &&
                    (x.Status == UserReportStatus.Pending ||
                     x.Status == UserReportStatus.UnderReview),
                cancellationToken);

        if (hasActiveReport)
        {
            throw new ConflictException(
                "Bu istifadəçi və məzmun üçün artıq baxılmaqda olan şikayətiniz var.");
        }

        var last24Hours = DateTime.UtcNow.AddHours(-24);
        var recentReportCount = await _context.UserReports
            .AsNoTracking()
            .CountAsync(x =>
                    x.ReporterUserId == reporterUserId &&
                    x.CreatedAtUtc >= last24Hours,
                cancellationToken);

        if (recentReportCount >= MaxReportsPer24Hours)
        {
            throw new ConflictException(
                "24 saat ərzində maksimum şikayət limitinə çatmısınız.");
        }

        var savedEvidence = new List<(string StorageKey, string OriginalFileName)>();
        var reportWasCommitted = false;

        try
        {
            foreach (var evidenceImage in dto.EvidenceImages)
            {
                var storageKey = await _fileStorageService
                    .SaveReportEvidenceImageAsync(
                        evidenceImage,
                        cancellationToken);
                savedEvidence.Add((
                    storageKey,
                    NormalizeFileName(evidenceImage.FileName)));
            }

            var report = new UserReport
            {
                ReporterUserId = reporterUserId,
                ReportedUserId = reportedUserId,
                Category = dto.Category,
                Description = description,
                EvidenceImageStorageKey = null,
                ReportedContentType = contentReference.ContentType,
                ReportedContentId = contentReference.ContentId,
                ReportedContentSnapshot = contentReference.Snapshot,
                ReportedContentUrl = contentReference.Url,
                Status = UserReportStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            for (var index = 0; index < savedEvidence.Count; index++)
            {
                report.EvidenceImages.Add(new UserReportEvidence
                {
                    StorageKey = savedEvidence[index].StorageKey,
                    OriginalFileName = savedEvidence[index].OriginalFileName,
                    DisplayOrder = index,
                    CreatedAtUtc = report.CreatedAtUtc
                });
            }

            await _context.UserReports.AddAsync(report, cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException(
                    "Bu istifadəçi və məzmun üçün artıq baxılmaqda olan şikayətiniz var.");
            }

            reportWasCommitted = true;

            return new UserReportDTO
            {
                Id = report.Id,
                ReportedUserId = reportedUserId,
                ReportedUserName = reportedUser.UserName,
                Category = report.Category,
                Description = report.Description,
                HasEvidenceImage = savedEvidence.Count > 0,
                EvidenceImageCount = savedEvidence.Count,
                ReportedContentType = report.ReportedContentType,
                ReportedContentId = report.ReportedContentId,
                ReportedContentSnapshot = report.ReportedContentSnapshot,
                ReportedContentUrl = report.ReportedContentUrl,
                Status = report.Status,
                CreatedAtUtc = report.CreatedAtUtc
            };
        }
        catch
        {
            if (!reportWasCommitted)
            {
                foreach (var evidence in savedEvidence)
                {
                    await _fileStorageService.DeleteReportEvidenceImageAsync(
                        evidence.StorageKey);
                }
            }

            throw;
        }
    }

    private async Task<ReportedContentReference> ResolveReportedContentAsync(
        UserReportContentType contentType,
        long? contentId,
        Guid reportedUserId,
        string reportedUserName,
        CancellationToken cancellationToken)
    {
        if (contentType == UserReportContentType.UserProfile)
        {
            if (contentId.HasValue)
            {
                throw new BadRequestException(
                    "Profil şikayəti üçün məzmun ID-si göndərilməməlidir.");
            }

            return new ReportedContentReference(
                contentType,
                null,
                $"İstifadəçi profili: @{reportedUserName}",
                $"/users/{Uri.EscapeDataString(reportedUserName)}");
        }

        if (!contentId.HasValue || contentId.Value <= 0)
        {
            throw new BadRequestException(
                "Seçilən məzmun növü üçün düzgün məzmun ID-si göndərilməlidir.");
        }

        var id = contentId.Value;

        return contentType switch
        {
            UserReportContentType.Post => await ResolvePostAsync(
                id,
                reportedUserId,
                cancellationToken),

            UserReportContentType.PostComment => await ResolvePostCommentAsync(
                id,
                reportedUserId,
                cancellationToken),

            UserReportContentType.Review => await ResolveReviewAsync(
                id,
                reportedUserId,
                cancellationToken),

            UserReportContentType.ReviewComment => await ResolveReviewCommentAsync(
                id,
                reportedUserId,
                cancellationToken),

            UserReportContentType.Discussion => await ResolveDiscussionAsync(
                id,
                reportedUserId,
                cancellationToken),

            UserReportContentType.DiscussionReply => await ResolveDiscussionReplyAsync(
                id,
                reportedUserId,
                cancellationToken),

            UserReportContentType.Community => await ResolveCommunityAsync(
                id,
                reportedUserId,
                cancellationToken),

            UserReportContentType.DirectMessage => throw new BadRequestException(
                "Mesaj şikayəti hələ aktiv deyil."),

            _ => throw new BadRequestException(
                "Şikayət edilən məzmun növü dəstəklənmir.")
        };
    }

    private async Task<ReportedContentReference> ResolvePostAsync(
        long postId,
        Guid reportedUserId,
        CancellationToken cancellationToken)
    {
        var post = await _context.UserPosts
            .AsNoTracking()
            .Where(x => x.Id == postId)
            .Select(x => new { x.UserId, x.Content, x.ImageUrl })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şikayət edilən post tapılmadı.");

        EnsureContentOwner(post.UserId, reportedUserId);

        var snapshot = post.Content ??
            (post.ImageUrl is null ? "Post məzmunu yoxdur." : "Şəkilli post");

        return new ReportedContentReference(
            UserReportContentType.Post,
            postId,
            CreateSnapshot(snapshot),
            null);
    }

    private async Task<ReportedContentReference> ResolvePostCommentAsync(
        long commentId,
        Guid reportedUserId,
        CancellationToken cancellationToken)
    {
        var comment = await _context.PostComments
            .AsNoTracking()
            .Where(x => x.Id == commentId)
            .Select(x => new { x.UserId, x.Content })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şikayət edilən post şərhi tapılmadı.");

        EnsureContentOwner(comment.UserId, reportedUserId);

        return new ReportedContentReference(
            UserReportContentType.PostComment,
            commentId,
            CreateSnapshot(comment.Content),
            null);
    }

    private async Task<ReportedContentReference> ResolveReviewAsync(
        long reviewId,
        Guid reportedUserId,
        CancellationToken cancellationToken)
    {
        var review = await _context.Reviews
            .AsNoTracking()
            .Where(x => x.Id == reviewId)
            .Select(x => new { x.UserId, x.Content })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şikayət edilən rəy tapılmadı.");

        EnsureContentOwner(review.UserId, reportedUserId);

        return new ReportedContentReference(
            UserReportContentType.Review,
            reviewId,
            CreateSnapshot(review.Content),
            null);
    }

    private async Task<ReportedContentReference> ResolveReviewCommentAsync(
        long commentId,
        Guid reportedUserId,
        CancellationToken cancellationToken)
    {
        var comment = await _context.ReviewComments
            .AsNoTracking()
            .Where(x => x.Id == commentId)
            .Select(x => new { x.UserId, x.Content })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şikayət edilən rəy şərhi tapılmadı.");

        EnsureContentOwner(comment.UserId, reportedUserId);

        return new ReportedContentReference(
            UserReportContentType.ReviewComment,
            commentId,
            CreateSnapshot(comment.Content),
            null);
    }

    private async Task<ReportedContentReference> ResolveDiscussionAsync(
        long discussionId,
        Guid reportedUserId,
        CancellationToken cancellationToken)
    {
        if (discussionId > int.MaxValue)
            throw new BadRequestException("Forum mövzusu ID-si düzgün deyil.");

        var discussion = await _context.Discussions
            .AsNoTracking()
            .Where(x => x.Id == (int)discussionId)
            .Select(x => new { x.UserId, x.Title, x.Content, x.Slug })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şikayət edilən forum mövzusu tapılmadı.");

        EnsureContentOwner(discussion.UserId, reportedUserId);

        return new ReportedContentReference(
            UserReportContentType.Discussion,
            discussionId,
            CreateSnapshot($"{discussion.Title}\n{discussion.Content}"),
            $"/forum/discussions/{Uri.EscapeDataString(discussion.Slug)}");
    }

    private async Task<ReportedContentReference> ResolveDiscussionReplyAsync(
        long replyId,
        Guid reportedUserId,
        CancellationToken cancellationToken)
    {
        if (replyId > int.MaxValue)
            throw new BadRequestException("Forum cavabı ID-si düzgün deyil.");

        var reply = await _context.DiscussionReplies
            .AsNoTracking()
            .Where(x => x.Id == (int)replyId)
            .Select(x => new
            {
                x.UserId,
                x.Content,
                DiscussionSlug = x.Discussion.Slug
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şikayət edilən forum cavabı tapılmadı.");

        EnsureContentOwner(reply.UserId, reportedUserId);

        return new ReportedContentReference(
            UserReportContentType.DiscussionReply,
            replyId,
            CreateSnapshot(reply.Content),
            $"/forum/discussions/{Uri.EscapeDataString(reply.DiscussionSlug)}#reply-{replyId}");
    }

    private async Task<ReportedContentReference> ResolveCommunityAsync(
        long communityId,
        Guid reportedUserId,
        CancellationToken cancellationToken)
    {
        if (communityId > int.MaxValue)
            throw new BadRequestException("İcma ID-si düzgün deyil.");

        var community = await _context.ForumCommunities
            .AsNoTracking()
            .Where(x => x.Id == (int)communityId)
            .Select(x => new
            {
                x.OwnerId,
                x.Name,
                x.Description,
                x.Slug
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şikayət edilən icma tapılmadı.");

        EnsureContentOwner(community.OwnerId, reportedUserId);

        return new ReportedContentReference(
            UserReportContentType.Community,
            communityId,
            CreateSnapshot($"{community.Name}\n{community.Description}"),
            $"/forum/communities/{Uri.EscapeDataString(community.Slug)}");
    }

    private static void EnsureContentOwner(
        Guid actualOwnerUserId,
        Guid reportedUserId)
    {
        if (actualOwnerUserId != reportedUserId)
        {
            throw new BadRequestException(
                "Seçilən məzmun şikayət edilən istifadəçiyə aid deyil.");
        }
    }

    private static string CreateSnapshot(string value)
    {
        var normalized = string.Join(
            " ",
            value.Split(
                ['\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (normalized.Length <= MaxContentSnapshotLength)
            return normalized;

        return normalized[..MaxContentSnapshotLength];
    }

    private static UserReportCategoryOptionDTO CreateCategoryOption(
        UserReportCategory value,
        string label)
    {
        return new UserReportCategoryOptionDTO
        {
            Value = value,
            Label = label
        };
    }

    private static string NormalizeFileName(string fileName)
    {
        var normalized = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalized))
            return "evidence";
        return normalized.Length <= 260 ? normalized : normalized[..260];
    }

    private sealed record ReportedContentReference(
        UserReportContentType ContentType,
        long? ContentId,
        string? Snapshot,
        string? Url);
}
