using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ludora.Persistence.Services.Admin;

public sealed class AdminModerationAuditService : IAdminModerationAuditService
{
    private const int MaxReturnedItems = 500;
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 200;

    private static readonly HashSet<string> NamedCatalogResourceTypes =
        new(StringComparer.Ordinal)
        {
            "Oyun",
            "Janr",
            "Platforma"
        };

    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;
    private readonly ILogger<AdminModerationAuditService> _logger;

    public AdminModerationAuditService(
        AppDbContext context,
        IIgdbClient igdbClient,
        ILogger<AdminModerationAuditService> logger)
    {
        _context = context;
        _igdbClient = igdbClient;
        _logger = logger;
    }

    public async Task<PagedResultDTO<ModeratorActionLogDTO>> GetAllAsync(
        AdminModerationAuditFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.PageNumber < 1)
            throw new BadRequestException(
                "Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (filter.PageSize is < 1 or > MaxPageSize)
            throw new BadRequestException(
                $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");

        if (filter.FromUtc.HasValue &&
            filter.ToUtc.HasValue &&
            filter.FromUtc.Value > filter.ToUtc.Value)
        {
            throw new BadRequestException(
                "Başlanğıc tarixi bitiş tarixindən sonra ola bilməz.");
        }

        var search = string.IsNullOrWhiteSpace(filter.Search)
            ? null
            : filter.Search.Trim();

        if (search?.Length > MaxSearchLength)
            throw new BadRequestException(
                $"Axtarış mətni maksimum {MaxSearchLength} simvol ola bilər.");

        var query = ProjectLogs();

        if (filter.AdminUserId.HasValue)
            query = query.Where(x => x.AdminUserId == filter.AdminUserId.Value);

        if (filter.TargetUserId.HasValue)
            query = query.Where(x => x.TargetUserId == filter.TargetUserId.Value);

        if (filter.UserReportId.HasValue)
            query = query.Where(x => x.UserReportId == filter.UserReportId.Value);

        if (filter.ActionType.HasValue)
            query = query.Where(x => x.ActionType == filter.ActionType.Value);

        if (filter.FromUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc <= filter.ToUtc.Value);

        if (search is not null)
        {
            query = query.Where(x =>
                x.AdminUserName.Contains(search) ||
                x.TargetUserName.Contains(search) ||
                (x.Operation != null && x.Operation.Contains(search)) ||
                (x.ResourceType != null && x.ResourceType.Contains(search)) ||
                (x.ResourceId != null && x.ResourceId.Contains(search)) ||
                (x.ResourceName != null && x.ResourceName.Contains(search)) ||
                (x.Reason != null && x.Reason.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        items = await EnrichResourceDetailsAsync(items, cancellationToken);

        return new PagedResultDTO<ModeratorActionLogDTO>
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

    public async Task<IReadOnlyList<ModeratorActionLogDTO>> GetByReportAsync(
        long reportId,
        CancellationToken cancellationToken = default)
    {
        if (reportId <= 0)
            throw new BadRequestException("Şikayət ID-si düzgün deyil.");

        var reportExists = await _context.UserReports
            .AsNoTracking()
            .AnyAsync(x => x.Id == reportId, cancellationToken);

        if (!reportExists)
            throw new NotFoundException("Şikayət tapılmadı.");

        var items = await ProjectLogs()
            .Where(x => x.UserReportId == reportId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(MaxReturnedItems)
            .ToListAsync(cancellationToken);

        return await EnrichResourceDetailsAsync(items, cancellationToken);
    }

    public async Task<IReadOnlyList<ModeratorActionLogDTO>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new BadRequestException("İstifadəçi ID-si düzgün deyil.");

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var items = await ProjectLogs()
            .Where(x => x.TargetUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(MaxReturnedItems)
            .ToListAsync(cancellationToken);

        return await EnrichResourceDetailsAsync(items, cancellationToken);
    }

    private IQueryable<ModeratorActionLogDTO> ProjectLogs()
    {
        return _context.ModeratorActionLogs
            .AsNoTracking()
            .Select(x => new ModeratorActionLogDTO
            {
                Id = x.Id,
                AdminUserId = x.AdminUserId,
                AdminUserName = x.AdminUser.UserName ?? string.Empty,
                TargetUserId = x.TargetUserId,
                TargetUserName = x.TargetUser != null
                    ? x.TargetUser.UserName ?? string.Empty
                    : string.Empty,
                UserReportId = x.UserReportId,
                UserWarningId = x.UserWarningId,
                UserBanHistoryId = x.UserBanHistoryId,
                ActionType = x.ActionType,
                PreviousReportStatus = x.PreviousReportStatus,
                NewReportStatus = x.NewReportStatus,
                Operation = x.Operation,
                ResourceType = x.ResourceType,
                ResourceId = x.ResourceId,
                ResourceName = x.ResourceName,
                Reason = x.Reason,
                CreatedAtUtc = x.CreatedAtUtc
            });
    }

    private async Task<List<ModeratorActionLogDTO>> EnrichResourceDetailsAsync(
        IReadOnlyList<ModeratorActionLogDTO> items,
        CancellationToken cancellationToken)
    {
        var keys = items
            .Where(NeedsResourceEnrichment)
            .Select(ToResourceKey)
            .Where(x => x.HasValue)
            .Select(x => x.GetValueOrDefault())
            .Distinct()
            .ToArray();

        if (keys.Length == 0)
            return items.ToList();

        var details = new Dictionary<ResourceKey, ResourceDetails>();

        await LoadPostDetailsAsync(keys, details, cancellationToken);
        await LoadGameDetailsAsync(keys, details, cancellationToken);
        await LoadGenreDetailsAsync(keys, details, cancellationToken);
        await LoadPlatformDetailsAsync(keys, details, cancellationToken);

        return items
            .Select(item => ApplyResourceDetails(item, details))
            .ToList();
    }

    private static bool NeedsResourceEnrichment(ModeratorActionLogDTO item)
    {
        if (string.IsNullOrWhiteSpace(item.ResourceType) ||
            string.IsNullOrWhiteSpace(item.ResourceId))
        {
            return false;
        }

        if (item.ResourceType == "Post")
        {
            return !item.TargetUserId.HasValue ||
                   string.IsNullOrWhiteSpace(item.TargetUserName);
        }

        return NamedCatalogResourceTypes.Contains(item.ResourceType) &&
               string.IsNullOrWhiteSpace(item.ResourceName);
    }

    private static ResourceKey? ToResourceKey(ModeratorActionLogDTO item)
    {
        return long.TryParse(item.ResourceId, out var id) && id > 0
            ? new ResourceKey(item.ResourceType!, id)
            : null;
    }

    private static ModeratorActionLogDTO ApplyResourceDetails(
        ModeratorActionLogDTO item,
        IReadOnlyDictionary<ResourceKey, ResourceDetails> details)
    {
        var key = ToResourceKey(item);
        if (!key.HasValue || !details.TryGetValue(key.Value, out var value))
            return item;

        return item with
        {
            ResourceName = string.IsNullOrWhiteSpace(item.ResourceName)
                ? value.Name
                : item.ResourceName,
            TargetUserId = item.TargetUserId ?? value.OwnerUserId,
            TargetUserName = string.IsNullOrWhiteSpace(item.TargetUserName)
                ? value.OwnerUserName ?? string.Empty
                : item.TargetUserName
        };
    }

    private async Task LoadPostDetailsAsync(
        IReadOnlyCollection<ResourceKey> keys,
        IDictionary<ResourceKey, ResourceDetails> details,
        CancellationToken cancellationToken)
    {
        var ids = keys
            .Where(x => x.ResourceType == "Post")
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
            return;

        var posts = await _context.UserPosts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.UserId,
                UserName = x.User.UserName ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        foreach (var post in posts)
        {
            details[new ResourceKey("Post", post.Id)] =
                new ResourceDetails(null, post.UserId, post.UserName);
        }
    }

    private async Task LoadGameDetailsAsync(
        IReadOnlyCollection<ResourceKey> keys,
        IDictionary<ResourceKey, ResourceDetails> details,
        CancellationToken cancellationToken)
    {
        var ids = GetResourceIds(keys, "Oyun");
        if (ids.Length == 0)
            return;

        var localIds = ids
            .Where(x => x <= int.MaxValue)
            .Select(x => (int)x)
            .ToArray();

        var localItems = await _context.Games
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => localIds.Contains(x.Id))
            .Select(x => new { Id = (long)x.Id, x.Name })
            .ToListAsync(cancellationToken);

        foreach (var item in localItems)
            details[new ResourceKey("Oyun", item.Id)] = new(item.Name, null, null);

        var unresolved = GetUnresolvedIds(ids, "Oyun", details);
        if (unresolved.Length == 0)
            return;

        var overrides = await _context.IgdbGameOverrides
            .AsNoTracking()
            .Where(x => unresolved.Contains(x.IgdbGameId) && x.Name != null)
            .Select(x => new { Id = x.IgdbGameId, x.Name })
            .ToListAsync(cancellationToken);

        foreach (var item in overrides)
        {
            if (!string.IsNullOrWhiteSpace(item.Name))
                details[new ResourceKey("Oyun", item.Id)] = new(item.Name, null, null);
        }

        unresolved = GetUnresolvedIds(ids, "Oyun", details);
        if (unresolved.Length == 0 || !_igdbClient.IsEnabled)
            return;

        try
        {
            var igdbItems = await _igdbClient.GetGamesByIdsAsync(
                unresolved,
                cancellationToken);

            foreach (var item in igdbItems)
                details[new ResourceKey("Oyun", item.Id)] = new(item.Name, null, null);
        }
        catch (ExternalServiceException exception)
        {
            LogIgdbLookupFailure(exception, "Oyun", unresolved.Length);
        }
    }

    private async Task LoadGenreDetailsAsync(
        IReadOnlyCollection<ResourceKey> keys,
        IDictionary<ResourceKey, ResourceDetails> details,
        CancellationToken cancellationToken)
    {
        var ids = GetResourceIds(keys, "Janr");
        if (ids.Length == 0)
            return;

        var localIds = ids
            .Where(x => x <= int.MaxValue)
            .Select(x => (int)x)
            .ToArray();

        var localItems = await _context.Genres
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => localIds.Contains(x.Id))
            .Select(x => new { Id = (long)x.Id, x.Name })
            .ToListAsync(cancellationToken);

        foreach (var item in localItems)
            details[new ResourceKey("Janr", item.Id)] = new(item.Name, null, null);

        var unresolved = GetUnresolvedIds(ids, "Janr", details);
        if (unresolved.Length == 0)
            return;

        var overrides = await _context.IgdbGenreOverrides
            .AsNoTracking()
            .Where(x => unresolved.Contains(x.IgdbGenreId) && x.Name != null)
            .Select(x => new { Id = x.IgdbGenreId, x.Name })
            .ToListAsync(cancellationToken);

        foreach (var item in overrides)
        {
            if (!string.IsNullOrWhiteSpace(item.Name))
                details[new ResourceKey("Janr", item.Id)] = new(item.Name, null, null);
        }

        unresolved = GetUnresolvedIds(ids, "Janr", details);
        if (unresolved.Length == 0 || !_igdbClient.IsEnabled)
            return;

        try
        {
            var igdbItems = await _igdbClient.GetGenresByIdsAsync(
                unresolved,
                cancellationToken);

            foreach (var item in igdbItems)
                details[new ResourceKey("Janr", item.Id)] = new(item.Name, null, null);
        }
        catch (ExternalServiceException exception)
        {
            LogIgdbLookupFailure(exception, "Janr", unresolved.Length);
        }
    }

    private async Task LoadPlatformDetailsAsync(
        IReadOnlyCollection<ResourceKey> keys,
        IDictionary<ResourceKey, ResourceDetails> details,
        CancellationToken cancellationToken)
    {
        var ids = GetResourceIds(keys, "Platforma");
        if (ids.Length == 0)
            return;

        var localIds = ids
            .Where(x => x <= int.MaxValue)
            .Select(x => (int)x)
            .ToArray();

        var localItems = await _context.Platforms
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => localIds.Contains(x.Id))
            .Select(x => new { Id = (long)x.Id, x.Name })
            .ToListAsync(cancellationToken);

        foreach (var item in localItems)
            details[new ResourceKey("Platforma", item.Id)] = new(item.Name, null, null);

        var unresolved = GetUnresolvedIds(ids, "Platforma", details);
        if (unresolved.Length == 0)
            return;

        var overrides = await _context.IgdbPlatformOverrides
            .AsNoTracking()
            .Where(x => unresolved.Contains(x.IgdbPlatformId) && x.Name != null)
            .Select(x => new { Id = x.IgdbPlatformId, x.Name })
            .ToListAsync(cancellationToken);

        foreach (var item in overrides)
        {
            if (!string.IsNullOrWhiteSpace(item.Name))
                details[new ResourceKey("Platforma", item.Id)] = new(item.Name, null, null);
        }

        unresolved = GetUnresolvedIds(ids, "Platforma", details);
        if (unresolved.Length == 0 || !_igdbClient.IsEnabled)
            return;

        try
        {
            var igdbItems = await _igdbClient.GetPlatformsByIdsAsync(
                unresolved,
                cancellationToken);

            foreach (var item in igdbItems)
                details[new ResourceKey("Platforma", item.Id)] = new(item.Name, null, null);
        }
        catch (ExternalServiceException exception)
        {
            LogIgdbLookupFailure(exception, "Platforma", unresolved.Length);
        }
    }

    private static long[] GetResourceIds(
        IEnumerable<ResourceKey> keys,
        string resourceType)
    {
        return keys
            .Where(x => x.ResourceType == resourceType)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();
    }

    private static long[] GetUnresolvedIds(
        IEnumerable<long> ids,
        string resourceType,
        IDictionary<ResourceKey, ResourceDetails> details)
    {
        return ids
            .Where(id => !details.ContainsKey(new ResourceKey(resourceType, id)))
            .ToArray();
    }

    private void LogIgdbLookupFailure(
        Exception exception,
        string resourceType,
        int resourceCount)
    {
        _logger.LogWarning(
            exception,
            "Audit jurnalındakı köhnə resurs adları IGDB-dən alınmadı. ResourceType: {ResourceType}, ResourceCount: {ResourceCount}",
            resourceType,
            resourceCount);
    }

    private readonly record struct ResourceKey(string ResourceType, long Id);

    private sealed record ResourceDetails(
        string? Name,
        Guid? OwnerUserId,
        string? OwnerUserName);
}
