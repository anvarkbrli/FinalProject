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
    private const int MaxReportsPer24Hours = 10;

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

        var description = dto.Description.NormalizeOptionalText(
            "Şikayət açıqlaması",
            MaxDescriptionLength);

        if (description is not null && description.Length < MinDescriptionLength)
        {
            throw new BadRequestException(
                $"Şikayət açıqlaması ən azı {MinDescriptionLength} simvol olmalıdır.");
        }

        if (description is null && dto.EvidenceImage is null)
        {
            throw new BadRequestException(
                "Sübut kimi ən azı açıqlama və ya şəkil əlavə edilməlidir.");
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

        var hasActiveReport = await _context.UserReports
            .AsNoTracking()
            .AnyAsync(x =>
                    x.ReporterUserId == reporterUserId &&
                    x.ReportedUserId == reportedUserId &&
                    (x.Status == UserReportStatus.Pending ||
                     x.Status == UserReportStatus.UnderReview),
                cancellationToken);

        if (hasActiveReport)
        {
            throw new ConflictException(
                "Bu istifadəçi üçün artıq baxılmaqda olan şikayətiniz var.");
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

        string? evidenceImageUrl = null;
        var reportWasCommitted = false;

        try
        {
            if (dto.EvidenceImage is not null)
            {
                evidenceImageUrl = await _fileStorageService
                    .SaveReportEvidenceImageAsync(dto.EvidenceImage);
            }

            var report = new UserReport
            {
                ReporterUserId = reporterUserId,
                ReportedUserId = reportedUserId,
                Category = dto.Category,
                Description = description,
                EvidenceImageStorageKey = evidenceImageUrl,
                Status = UserReportStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.UserReports.AddAsync(report, cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException(
                    "Bu istifadəçi üçün artıq baxılmaqda olan şikayətiniz var.");
            }

            reportWasCommitted = true;

            return new UserReportDTO
            {
                Id = report.Id,
                ReportedUserId = reportedUserId,
                ReportedUserName = reportedUser.UserName,
                Category = report.Category,
                Description = report.Description,
                HasEvidenceImage = report.EvidenceImageStorageKey is not null,
                Status = report.Status,
                CreatedAtUtc = report.CreatedAtUtc
            };
        }
        catch
        {
            if (!reportWasCommitted && evidenceImageUrl is not null)
                await _fileStorageService.DeleteReportEvidenceImageAsync(evidenceImageUrl);

            throw;
        }
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
}
