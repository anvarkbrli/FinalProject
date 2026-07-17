using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class UserWarningService : IUserWarningService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;

    public UserWarningService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDTO<UserWarningDTO>> GetMineAsync(
        Guid userId,
        bool? isRead,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidatePagination(pageNumber, pageSize);

        var query = _context.UserWarnings
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (isRead.HasValue)
            query = query.Where(x => x.IsRead == isRead.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await Project(query)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDTO<UserWarningDTO>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<UserWarningDTO> MarkAsReadAsync(
        Guid userId,
        int warningId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        if (warningId <= 0)
            throw new BadRequestException("Xəbərdarlıq ID-si düzgün deyil.");

        var warning = await _context.UserWarnings
            .FirstOrDefaultAsync(
                x => x.Id == warningId && x.UserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("Xəbərdarlıq tapılmadı.");

        if (!warning.IsRead)
        {
            warning.IsRead = true;
            warning.ReadAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }

        return await Project(
                _context.UserWarnings
                    .AsNoTracking()
                    .Where(x => x.Id == warningId))
            .FirstAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var now = DateTime.UtcNow;

        await _context.UserWarnings
            .Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.IsRead, true)
                    .SetProperty(x => x.ReadAtUtc, now),
                cancellationToken);
    }

    private static IQueryable<UserWarningDTO> Project(
        IQueryable<UserWarning> query)
    {
        return query.Select(x => new UserWarningDTO
        {
            Id = x.Id,
            UserId = x.UserId,
            UserName = x.User.UserName ?? string.Empty,
            WarnedByUserId = x.WarnedByUserId,
            WarnedByUserName = x.WarnedByUser.UserName ?? string.Empty,
            Message = x.Message,
            CreatedAtUtc = x.CreatedAtUtc,
            IsRead = x.IsRead,
            ReadAtUtc = x.ReadAtUtc
        });
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new BadRequestException("İstifadəçi ID-si düzgün deyil.");
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");

        if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new BadRequestException("Səhifə parametrləri həddindən artıq böyükdür.");
    }
}
