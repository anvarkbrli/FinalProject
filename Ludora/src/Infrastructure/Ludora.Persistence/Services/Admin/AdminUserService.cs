using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Admin;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services.Admin;

public sealed class AdminUserService : IAdminUserService
{
    private const int MaxSearchLength = 200;
    private const int MaxPageSize = 100;
    private const int MaxBanDurationMinutes = 525600;

    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public AdminUserService(
        AppDbContext context,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<PagedResultDTO<AdminUserListItemDTO>> GetAllAsync(
        AdminUserFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidatePagination(filter.PageNumber, filter.PageSize);

        var search = filter.Search.NormalizeOptionalText("Axtarış mətni", MaxSearchLength);
        var role = NormalizeOptionalRole(filter.Role);
        var now = DateTime.UtcNow;

        var query = _context.Users.AsNoTracking();

        if (search is not null)
        {
            query = query.Where(x =>
                (x.UserName != null && x.UserName.Contains(search)) ||
                (x.Email != null && x.Email.Contains(search)));
        }

        if (role is not null)
        {
            query = query.Where(user =>
                _context.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(
                        _context.Roles,
                        userRole => userRole.RoleId,
                        identityRole => identityRole.Id,
                        (_, identityRole) => identityRole.Name)
                    .Any(roleName => roleName == role));
        }

        if (filter.IsBanned.HasValue)
        {
            query = filter.IsBanned.Value
                ? query.Where(x =>
                    x.IsPermanentlyBanned ||
                    (x.BannedUntilUtc.HasValue && x.BannedUntilUtc > now))
                : query.Where(x =>
                    !x.IsPermanentlyBanned &&
                    (!x.BannedUntilUtc.HasValue || x.BannedUntilUtc <= now));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, filter.SortBy);

        var items = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(user => new AdminUserListItemDTO
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                ProfileImageUrl = user.ProfileImageUrl,
                Role = _context.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(
                        _context.Roles,
                        userRole => userRole.RoleId,
                        identityRole => identityRole.Id,
                        (_, identityRole) => identityRole.Name)
                    .OrderBy(roleName => roleName)
                    .FirstOrDefault() ?? RoleNames.User,
                IsPrivate = user.IsPrivate,
                IsBanned = user.IsPermanentlyBanned ||
                           (user.BannedUntilUtc.HasValue && user.BannedUntilUtc > now),
                IsPermanentlyBanned = user.IsPermanentlyBanned,
                BannedUntilUtc = user.BannedUntilUtc,
                CreatedAt = user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDTO<AdminUserListItemDTO>
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

    public async Task<AdminUserDetailDTO> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new BadRequestException("İstifadəçi ID-si düzgün deyil.");

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var role = await _context.UserRoles
            .Where(x => x.UserId == userId)
            .Join(
                _context.Roles,
                userRole => userRole.RoleId,
                identityRole => identityRole.Id,
                (_, identityRole) => identityRole.Name)
            .OrderBy(x => x)
            .FirstOrDefaultAsync(cancellationToken)
            ?? RoleNames.User;

        var now = DateTime.UtcNow;

        return new AdminUserDetailDTO
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            Role = role,
            IsPrivate = user.IsPrivate,
            IsBanned = user.IsPermanentlyBanned ||
                       (user.BannedUntilUtc.HasValue && user.BannedUntilUtc > now),
            IsPermanentlyBanned = user.IsPermanentlyBanned,
            BannedUntilUtc = user.BannedUntilUtc,
            BanReason = user.BanReason,
            BannedAtUtc = user.BannedAtUtc,
            BannedByUserId = user.BannedByUserId,
            FollowerCount = await _context.UserFollows.CountAsync(x =>
                    x.FollowingId == userId && x.Status == FollowStatus.Accepted,
                cancellationToken),
            FollowingCount = await _context.UserFollows.CountAsync(x =>
                    x.FollowerId == userId && x.Status == FollowStatus.Accepted,
                cancellationToken),
            PostCount = await _context.UserPosts.CountAsync(x => x.UserId == userId, cancellationToken),
            DeletedPostCount = await _context.UserPosts
                .IgnoreQueryFilters()
                .CountAsync(x => x.UserId == userId && x.IsDeleted, cancellationToken),
            DiscussionCount = await _context.Discussions.CountAsync(x => x.UserId == userId, cancellationToken),
            ReplyCount = await _context.DiscussionReplies.CountAsync(x => x.UserId == userId, cancellationToken),
            CreatedAt = user.CreatedAt
        };
    }
    public async Task<AdminUserBanHistoryDTO> GetBanHistoryAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BadRequestException(
                "İstifadəçi ID-si düzgün deyil.");
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                UserName = x.UserName ?? string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                "İstifadəçi tapılmadı.");

        var now = DateTime.UtcNow;

        var histories = await _context.UserBanHistories
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.BannedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.BanType,
                x.Reason,
                x.BannedAtUtc,
                x.BannedUntilUtc,
                x.BannedByUserId,

                BannedByUserName =
                    x.BannedByUser.UserName ?? string.Empty,

                x.UnbannedAtUtc,
                x.UnbannedByUserId,

                UnbannedByUserName =
                    x.UnbannedByUser == null
                        ? null
                        : x.UnbannedByUser.UserName
            })
            .ToListAsync(cancellationToken);

        var items = histories
            .Select(history =>
            {
                var wasManuallyUnbanned =
                    history.UnbannedAtUtc.HasValue;

                var isActive =
                    !wasManuallyUnbanned &&
                    (
                        history.BanType == BanType.Permanent ||
                        (
                            history.BannedUntilUtc.HasValue &&
                            history.BannedUntilUtc.Value > now
                        )
                    );

                var isExpired =
                    !wasManuallyUnbanned &&
                    history.BanType == BanType.Temporary &&
                    history.BannedUntilUtc.HasValue &&
                    history.BannedUntilUtc.Value <= now;

                return new AdminUserBanHistoryItemDTO
                {
                    Id = history.Id,
                    Type = history.BanType,
                    Reason = history.Reason,
                    BannedAtUtc = history.BannedAtUtc,
                    BannedUntilUtc = history.BannedUntilUtc,
                    BannedByUserId = history.BannedByUserId,
                    BannedByUserName = history.BannedByUserName,
                    IsActive = isActive,
                    IsExpired = isExpired,
                    WasManuallyUnbanned = wasManuallyUnbanned,
                    UnbannedAtUtc = history.UnbannedAtUtc,
                    UnbannedByUserId = history.UnbannedByUserId,
                    UnbannedByUserName =
                        history.UnbannedByUserName
                };
            })
            .ToList();

        return new AdminUserBanHistoryDTO
        {
            UserId = user.Id,
            UserName = user.UserName,
            TotalBanCount = items.Count,

            TemporaryBanCount = items.Count(
                x => x.Type == BanType.Temporary),

            PermanentBanCount = items.Count(
                x => x.Type == BanType.Permanent),

            Items = items
        };
    }

    public async Task<AdminUserDetailDTO> BanAsync(
     Guid adminUserId,
     Guid userId,
     BanUserDTO dto,
     CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        EnsureDifferentUsers(
            adminUserId,
            userId,
            "Öz hesabınızı banlaya bilməzsiniz.");

        dto.Type.EnsureDefined("Ban növü");

        var reason = dto.Reason.NormalizeRequiredText(
            "Ban səbəbi",
            500);

        if (dto.Type == BanType.Temporary)
        {
            if (!dto.DurationMinutes.HasValue)
            {
                throw new BadRequestException(
                    "Müddətli ban üçün ban müddəti daxil edilməlidir.");
            }

            if (dto.DurationMinutes.Value <= 0 ||
                dto.DurationMinutes.Value > MaxBanDurationMinutes)
            {
                throw new BadRequestException(
                    $"Ban müddəti 1-{MaxBanDurationMinutes} dəqiqə aralığında olmalıdır.");
            }
        }

        if (dto.Type == BanType.Permanent &&
            dto.DurationMinutes.HasValue)
        {
            throw new BadRequestException(
                "Müddətsiz ban zamanı müddət göndərilməməlidir.");
        }

        var user = await _userManager.FindByIdAsync(
            userId.ToString())
            ?? throw new NotFoundException(
                "İstifadəçi tapılmadı.");

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(
            RoleNames.Admin,
            StringComparer.OrdinalIgnoreCase))
        {
            throw new ForbiddenException(
                "Admin rolundakı istifadəçini birbaşa banlamaq olmaz. Əvvəlcə rolunu dəyişin.");
        }

        var now = DateTime.UtcNow;

        var isCurrentlyBanned =
            user.IsPermanentlyBanned ||
            (
                user.BannedUntilUtc.HasValue &&
                user.BannedUntilUtc.Value > now
            );

        if (isCurrentlyBanned)
        {
            throw new ConflictException(
                "Bu istifadəçi artıq banlanıb. Yeni ban verməzdən əvvəl mövcud banı açın.");
        }

        DateTime? bannedUntilUtc = dto.Type == BanType.Temporary
          ? now.AddMinutes(dto.DurationMinutes!.Value)
          : null;

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        user.IsPermanentlyBanned =
            dto.Type == BanType.Permanent;

        user.BannedUntilUtc = bannedUntilUtc;
        user.BanReason = reason;
        user.BannedAtUtc = now;
        user.BannedByUserId = adminUserId;

        var updateResult =
            await _userManager.UpdateAsync(user);

        updateResult.ThrowIfFailed();

        var stampResult =
            await _userManager.UpdateSecurityStampAsync(user);

        stampResult.ThrowIfFailed();

        var history = new UserBanHistory
        {
            UserId = userId,
            BannedByUserId = adminUserId,
            BanType = dto.Type,
            Reason = reason,
            BannedAtUtc = now,
            BannedUntilUtc = bannedUntilUtc
        };

        await _context.UserBanHistories.AddAsync(
            history,
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return await GetByIdAsync(
            userId,
            cancellationToken);
    }

    public async Task<AdminUserDetailDTO> UnbanAsync(
       Guid adminUserId,
       Guid userId,
       CancellationToken cancellationToken = default)
    {
        EnsureDifferentUsers(
            adminUserId,
            userId,
            "Öz hesabınız üçün bu əməliyyatı edə bilməzsiniz.");

        var user = await _userManager.FindByIdAsync(
            userId.ToString())
            ?? throw new NotFoundException(
                "İstifadəçi tapılmadı.");

        var now = DateTime.UtcNow;

        var isCurrentlyBanned =
            user.IsPermanentlyBanned ||
            (
                user.BannedUntilUtc.HasValue &&
                user.BannedUntilUtc.Value > now
            );

        if (!isCurrentlyBanned)
        {
            throw new ConflictException(
                "Bu istifadəçinin aktiv banı yoxdur.");
        }

        var activeBanHistory =
            await _context.UserBanHistories
                .Where(x =>
                    x.UserId == userId &&
                    !x.UnbannedAtUtc.HasValue &&
                    (
                        x.BanType == BanType.Permanent ||
                        (
                            x.BannedUntilUtc.HasValue &&
                            x.BannedUntilUtc.Value > now
                        )
                    ))
                .OrderByDescending(x => x.BannedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        user.IsPermanentlyBanned = false;
        user.BannedUntilUtc = null;
        user.BanReason = null;
        user.BannedAtUtc = null;
        user.BannedByUserId = null;

        var updateResult =
            await _userManager.UpdateAsync(user);

        updateResult.ThrowIfFailed();

        var stampResult =
            await _userManager.UpdateSecurityStampAsync(user);

        stampResult.ThrowIfFailed();

        if (activeBanHistory is not null)
        {
            activeBanHistory.UnbannedAtUtc = now;
            activeBanHistory.UnbannedByUserId =
                adminUserId;

            await _context.SaveChangesAsync(
                cancellationToken);
        }

        await transaction.CommitAsync(
            cancellationToken);

        return await GetByIdAsync(
            userId,
            cancellationToken);
    }

    public async Task<AdminUserDetailDTO> ChangeRoleAsync(
        Guid adminUserId,
        Guid userId,
        ChangeUserRoleDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureDifferentUsers(adminUserId, userId, "Öz rolunuzu dəyişə bilməzsiniz.");

        var role = NormalizeRequiredRole(dto.Role);

        if (!await _roleManager.RoleExistsAsync(role))
            throw new NotFoundException($"{role} rolu sistemdə tapılmadı.");

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase) &&
            role != RoleNames.Admin)
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync(RoleNames.Admin);

            if (adminUsers.Count <= 1)
                throw new ConflictException("Sistemdəki son adminin rolunu dəyişmək olmaz.");
        }

        if (currentRoles.Count == 1 &&
            currentRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            return await GetByIdAsync(userId, cancellationToken);
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            removeResult.ThrowIfFailed();
        }

        var addResult = await _userManager.AddToRoleAsync(user, role);
        addResult.ThrowIfFailed();

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        stampResult.ThrowIfFailed();

        await transaction.CommitAsync(cancellationToken);

        return await GetByIdAsync(userId, cancellationToken);
    }

    private static IQueryable<AppUser> ApplySorting(
        IQueryable<AppUser> query,
        string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(x => x.CreatedAt),
            "username" => query.OrderBy(x => x.UserName),
            "username_desc" => query.OrderByDescending(x => x.UserName),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
    }

    private static string? NormalizeOptionalRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        return NormalizeRequiredRole(role);
    }

    private static string NormalizeRequiredRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new BadRequestException("Rol boş ola bilməz.");

        var trimmedRole = role.Trim();

        if (!RoleNames.All.Contains(trimmedRole))
            throw new BadRequestException("Yalnız Admin və User rolları istifadə oluna bilər.");

        return trimmedRole.Equals(RoleNames.Admin, StringComparison.OrdinalIgnoreCase)
            ? RoleNames.Admin
            : RoleNames.User;
    }

    private static void EnsureDifferentUsers(
        Guid adminUserId,
        Guid targetUserId,
        string message)
    {
        if (adminUserId == Guid.Empty || targetUserId == Guid.Empty)
            throw new BadRequestException("İstifadəçi ID-si düzgün deyil.");

        if (adminUserId == targetUserId)
            throw new ForbiddenException(message);
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
