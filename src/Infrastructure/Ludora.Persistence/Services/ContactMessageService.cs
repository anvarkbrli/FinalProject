using System.Net.Mail;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Contact;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class ContactMessageService : IContactMessageService
{
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 200;
    private static readonly IReadOnlySet<string> AllowedSubjects = new HashSet<string>(
        ["general", "account", "bug", "feedback", "moderation"],
        StringComparer.OrdinalIgnoreCase);

    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;

    public ContactMessageService(
        AppDbContext context,
        IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<ContactMessageDTO> CreateAsync(
        Guid? userId,
        ContactMessageCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var name = dto.Name.NormalizeRequiredText("Ad", 120);
        var email = NormalizeEmail(dto.Email);
        var subject = NormalizeSubject(dto.Subject);
        var message = dto.Message.NormalizeRequiredText("Mesaj", 5000);
        var language = NormalizeLanguage(dto.Language);

        if (message.Length < 10)
            throw new BadRequestException("Mesaj minimum 10 simvol olmalıdır.");

        Guid? normalizedUserId = null;
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId.Value, cancellationToken);

            if (userExists)
                normalizedUserId = userId.Value;
        }

        var duplicateCutoff = DateTime.UtcNow.AddMinutes(-3);
        var isDuplicate = await _context.ContactMessages
            .AsNoTracking()
            .AnyAsync(
                x => x.Email == email &&
                     x.Subject == subject &&
                     x.Message == message &&
                     x.CreatedAtUtc >= duplicateCutoff,
                cancellationToken);

        if (isDuplicate)
        {
            throw new ConflictException(
                language == "en"
                    ? "This message was already submitted recently."
                    : "Bu mesaj qısa müddət əvvəl artıq göndərilib.");
        }

        var entity = new ContactMessage
        {
            UserId = normalizedUserId,
            Name = name,
            Email = email,
            Subject = subject,
            Message = message,
            Language = language,
            Status = ContactMessageStatus.New,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.ContactMessages.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdForAdminAsync(entity.Id, cancellationToken);
    }

    public async Task<PagedResultDTO<ContactMessageDTO>> GetAllForAdminAsync(
        AdminContactMessageFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidatePagination(filter.PageNumber, filter.PageSize);

        var search = filter.Search.NormalizeOptionalText("Axtarış mətni", MaxSearchLength);
        var subject = filter.Subject.TrimOrNull()?.ToLowerInvariant();

        if (subject is not null && !AllowedSubjects.Contains(subject))
            throw new BadRequestException("Mövzu filtri düzgün deyil.");

        var query = _context.ContactMessages.AsNoTracking();

        if (search is not null)
        {
            query = query.Where(x =>
                x.Name.Contains(search) ||
                x.Email.Contains(search) ||
                x.Message.Contains(search) ||
                (x.ReplyBody != null && x.ReplyBody.Contains(search)));
        }

        if (subject is not null)
            query = query.Where(x => x.Subject == subject);

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);

        query = (filter.SortBy ?? "newest").Trim().ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(x => x.CreatedAtUtc),
            "status" => query.OrderBy(x => x.Status).ThenByDescending(x => x.CreatedAtUtc),
            _ => query.OrderByDescending(x => x.CreatedAtUtc)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var items = await MapAsync(entities, cancellationToken);
        return new PagedResultDTO<ContactMessageDTO>
        {
            Items = items,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)filter.PageSize)
        };
    }

    public async Task<ContactMessageDTO> GetByIdForAdminAsync(
        int contactMessageId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(contactMessageId);
        var entity = await _context.ContactMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contactMessageId, cancellationToken)
            ?? throw new NotFoundException("Əlaqə mesajı tapılmadı.");

        return (await MapAsync([entity], cancellationToken))[0];
    }

    public async Task<ContactMessageDTO> UpdateStatusAsync(
        int contactMessageId,
        ContactMessageStatus status,
        CancellationToken cancellationToken = default)
    {
        ValidateId(contactMessageId);
        if (status is ContactMessageStatus.ReplyQueued or ContactMessageStatus.Replied or ContactMessageStatus.ReplyFailed)
        {
            throw new BadRequestException(
                "Bu status email göndəriş sistemi tərəfindən idarə olunur.");
        }

        var entity = await _context.ContactMessages
            .FirstOrDefaultAsync(x => x.Id == contactMessageId, cancellationToken)
            ?? throw new NotFoundException("Əlaqə mesajı tapılmadı.");

        if (entity.Status == ContactMessageStatus.ReplyQueued)
            throw new ConflictException("Email cavabı göndəriş növbəsində olduğu üçün status dəyişdirilə bilməz.");

        if (entity.Status == ContactMessageStatus.Replied && status != ContactMessageStatus.Closed)
            throw new ConflictException("Cavablandırılmış mesaj yalnız bağlana bilər.");

        entity.Status = status;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.ClosedAtUtc = status == ContactMessageStatus.Closed
            ? DateTime.UtcNow
            : null;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetByIdForAdminAsync(contactMessageId, cancellationToken);
    }

    public async Task<ContactMessageDTO> ReplyAsync(
        int contactMessageId,
        Guid adminUserId,
        ContactMessageReplyDTO dto,
        CancellationToken cancellationToken = default)
    {
        ValidateId(contactMessageId);
        if (adminUserId == Guid.Empty)
            throw new BadRequestException("Admin istifadəçi müəyyən edilmədi.");

        ArgumentNullException.ThrowIfNull(dto);
        var subject = dto.Subject.NormalizeRequiredText("Email başlığı", 300);
        var body = dto.Body.NormalizeRequiredText("Cavab mətni", 5000);

        if (subject.Contains('\r') || subject.Contains('\n'))
            throw new BadRequestException("Email başlığı yeni sətir simvolu ehtiva edə bilməz.");

        if (body.Length < 5)
            throw new BadRequestException("Cavab mətni minimum 5 simvol olmalıdır.");

        var adminExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == adminUserId, cancellationToken);
        if (!adminExists)
            throw new NotFoundException("Admin istifadəçi tapılmadı.");

        var entity = await _context.ContactMessages
            .FirstOrDefaultAsync(x => x.Id == contactMessageId, cancellationToken)
            ?? throw new NotFoundException("Əlaqə mesajı tapılmadı.");

        if (entity.Status == ContactMessageStatus.ReplyQueued)
            throw new ConflictException("Bu mesaj üçün email cavabı artıq göndəriş növbəsindədir.");
        if (entity.Status == ContactMessageStatus.Replied)
            throw new ConflictException("Bu əlaqə mesajı artıq cavablandırılıb.");

        var now = DateTime.UtcNow;
        entity.RepliedByAdminUserId = adminUserId;
        entity.ReplySubject = subject;
        entity.ReplyBody = body;
        entity.ReplyQueuedAtUtc = now;
        entity.RepliedAtUtc = null;
        entity.ClosedAtUtc = null;
        entity.Status = ContactMessageStatus.ReplyQueued;
        entity.UpdatedAtUtc = now;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _emailService.SendContactMessageReplyAsync(
            entity.Email,
            entity.Name,
            subject,
            body,
            GetSubjectLabel(entity.Subject, entity.Language),
            entity.Message,
            entity.Language,
            entity.Id,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await GetByIdForAdminAsync(contactMessageId, cancellationToken);
    }

    private async Task<IReadOnlyList<ContactMessageDTO>> MapAsync(
        IReadOnlyCollection<ContactMessage> entities,
        CancellationToken cancellationToken)
    {
        var userIds = entities
            .SelectMany(x => new[] { x.UserId, x.RepliedByAdminUserId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        var userNames = userIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await _context.Users
                .AsNoTracking()
                .Where(x => userIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.UserName, cancellationToken);

        return entities.Select(x => new ContactMessageDTO
        {
            Id = x.Id,
            UserId = x.UserId,
            UserName = x.UserId.HasValue && userNames.TryGetValue(x.UserId.Value, out var userName)
                ? userName
                : null,
            Name = x.Name,
            Email = x.Email,
            Subject = x.Subject,
            Message = x.Message,
            Language = x.Language,
            Status = x.Status,
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc,
            RepliedByAdminUserId = x.RepliedByAdminUserId,
            RepliedByAdminUserName = x.RepliedByAdminUserId.HasValue &&
                                     userNames.TryGetValue(x.RepliedByAdminUserId.Value, out var adminName)
                ? adminName
                : null,
            ReplySubject = x.ReplySubject,
            ReplyBody = x.ReplyBody,
            ReplyQueuedAtUtc = x.ReplyQueuedAtUtc,
            RepliedAtUtc = x.RepliedAtUtc,
            ClosedAtUtc = x.ClosedAtUtc
        }).ToArray();
    }

    private static string NormalizeEmail(string? value)
    {
        var email = value.NormalizeRequiredText("Email", 320).ToLowerInvariant();
        try
        {
            var parsed = new MailAddress(email);
            if (!string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
                throw new FormatException();
        }
        catch (FormatException)
        {
            throw new BadRequestException("Email ünvanının formatı düzgün deyil.");
        }

        return email;
    }

    private static string NormalizeSubject(string? value)
    {
        var subject = value.NormalizeRequiredText("Mövzu", 40).ToLowerInvariant();
        if (!AllowedSubjects.Contains(subject))
            throw new BadRequestException("Seçilmiş mövzu düzgün deyil.");
        return subject;
    }

    private static string NormalizeLanguage(string? value) =>
        string.Equals(value?.Trim(), "en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "az";

    private static string GetSubjectLabel(string subject, string language)
    {
        var english = language == "en";
        return subject switch
        {
            "account" => english ? "Account and access" : "Hesab və giriş",
            "bug" => english ? "Bug report" : "Xəta bildirişi",
            "feedback" => english ? "Feedback and suggestion" : "Rəy və təklif",
            "moderation" => english ? "Safety and moderation" : "Təhlükəsizlik və moderasiya",
            _ => english ? "General question" : "Ümumi sual"
        };
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi minimum 1 olmalıdır.");
        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
    }

    private static void ValidateId(int id)
    {
        if (id <= 0)
            throw new BadRequestException("Əlaqə mesajı ID-si düzgün deyil.");
    }
}
