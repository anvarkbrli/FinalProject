using System.Text.Json;
using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Helpers;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities.Forum;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class ForumCommunityService : IForumCommunityService
{
    private const int MaxNameLength = 60;
    private const int MaxDescriptionLength = 1000;

    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly INotificationService _notificationService;
    private readonly IAccountDeletionService _accountDeletionService;

    public ForumCommunityService(
        AppDbContext context,
        IFileStorageService fileStorageService,
        INotificationService notificationService,
        IAccountDeletionService accountDeletionService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _notificationService = notificationService;
        _accountDeletionService = accountDeletionService;
    }

    public async Task<IReadOnlyList<ForumCommunitySummaryDTO>> GetAllAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ForumCommunities.AsNoTracking();

        query = query.Where(community =>
            community.Visibility != ForumCommunityVisibility.Private ||
            (currentUserId.HasValue &&
             community.Members.Any(member =>
                 member.UserId == currentUserId.Value &&
                 member.Status == ForumCommunityMembershipStatus.Active)));

        return await Project(query, currentUserId)
            .OrderByDescending(x => x.MemberCount)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumCommunitySummaryDTO>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await Project(
                _context.ForumCommunities
                    .AsNoTracking()
                    .Where(community => community.Members.Any(member =>
                        member.UserId == userId &&
                        member.Status == ForumCommunityMembershipStatus.Active)),
                userId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ForumCommunitySummaryDTO> GetBySlugAsync(
        string slug,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug
            .NormalizeRequiredText("İcma slug-u", 80)
            .ToLowerInvariant();

        var query = _context.ForumCommunities
            .AsNoTracking()
            .Where(x => x.Slug == normalizedSlug);

        var visibility = await query
            .Select(x => (ForumCommunityVisibility?)x.Visibility)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        if (visibility == ForumCommunityVisibility.Private)
        {
            if (!currentUserId.HasValue)
                throw new NotFoundException("İcma tapılmadı.");

            var hasAccess = await query.AnyAsync(community =>
                community.Members.Any(member =>
                    member.UserId == currentUserId.Value &&
                    (member.Status == ForumCommunityMembershipStatus.Active ||
                     member.Status == ForumCommunityMembershipStatus.Invited)),
                cancellationToken);

            if (!hasAccess)
                throw new NotFoundException("İcma tapılmadı.");
        }

        return await Project(query, currentUserId)
            .FirstAsync(cancellationToken);
    }

    public async Task<ForumCommunitySummaryDTO> CreateAsync(
        Guid userId,
        ForumCommunityCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!Enum.IsDefined(dto.Category))
            throw new BadRequestException("İcma kateqoriyası düzgün deyil.");
        if (!Enum.IsDefined(dto.Visibility))
            throw new BadRequestException("İcma görünürlüyü düzgün deyil.");

        var name = dto.Name.NormalizeRequiredText("İcma adı", MaxNameLength);
        var description = dto.Description.NormalizeRequiredText(
            "İcma açıqlaması",
            MaxDescriptionLength);

        if (!await _context.Users.AsNoTracking()
                .AnyAsync(x => x.Id == userId, cancellationToken))
        {
            throw new NotFoundException("İstifadəçi tapılmadı.");
        }

        if (await _context.ForumCommunities
                .IgnoreQueryFilters()
                .AnyAsync(x => x.Name == name, cancellationToken))
        {
            throw new ConflictException("Bu adda icma artıq mövcuddur.");
        }

        var community = new ForumCommunity
        {
            Name = name,
            Slug = await CreateUniqueSlugAsync(name, cancellationToken),
            Description = description,
            Category = dto.Category,
            Visibility = dto.Visibility,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow
        };

        var savedUrls = new List<string>();
        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            if (dto.Icon is not null)
            {
                community.IconUrl = await _fileStorageService
                    .SaveCommunityIconAsync(dto.Icon, cancellationToken);
                savedUrls.Add(community.IconUrl);
            }

            if (dto.CoverImage is not null)
            {
                community.CoverImageUrl = await _fileStorageService
                    .SaveCommunityCoverImageAsync(dto.CoverImage, cancellationToken);
                savedUrls.Add(community.CoverImageUrl);
            }

            community.Members.Add(new ForumCommunityMember
            {
                UserId = userId,
                Role = ForumCommunityMemberRole.Owner,
                Status = ForumCommunityMembershipStatus.Active,
                CreatedAt = DateTime.UtcNow
            });

            await _context.ForumCommunities.AddAsync(community, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var url in savedUrls)
                await _fileStorageService.DeleteAsync(url);
            throw;
        }

        return await Project(
                _context.ForumCommunities.AsNoTracking()
                    .Where(x => x.Id == community.Id),
                userId)
            .FirstAsync(cancellationToken);
    }

    public async Task JoinAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var community = await _context.ForumCommunities
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        var membership = await _context.ForumCommunityMembers
            .FirstOrDefaultAsync(
                x => x.CommunityId == id && x.UserId == userId,
                cancellationToken);

        if (membership?.Status == ForumCommunityMembershipStatus.Active)
        {
            await RemoveFollowIfExistsAsync(id, userId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (community.Visibility == ForumCommunityVisibility.Private)
        {
            if (membership?.Status == ForumCommunityMembershipStatus.Invited)
            {
                membership.Status = ForumCommunityMembershipStatus.Active;
                membership.UpdatedAt = DateTime.UtcNow;
                await RemoveFollowIfExistsAsync(id, userId, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            throw new ForbiddenException(
                "Gizli icmaya yalnız icma sahibinin dəvəti ilə qoşulmaq olar.");
        }

        var nextStatus = community.Visibility == ForumCommunityVisibility.Open
            ? ForumCommunityMembershipStatus.Active
            : ForumCommunityMembershipStatus.Pending;

        if (membership is null)
        {
            await _context.ForumCommunityMembers.AddAsync(
                new ForumCommunityMember
                {
                    CommunityId = id,
                    UserId = userId,
                    Role = ForumCommunityMemberRole.Member,
                    Status = nextStatus,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken);
        }
        else
        {
            membership.Status = nextStatus;
            membership.IsDeleted = false;
            membership.UpdatedAt = DateTime.UtcNow;
        }

        if (nextStatus == ForumCommunityMembershipStatus.Active)
        {
            await RemoveFollowIfExistsAsync(id, userId, cancellationToken);
        }
        else
        {
            await _notificationService.QueueAsync(
                new NotificationCreateRequest
                {
                    RecipientUserId = community.OwnerId,
                    ActorUserId = userId,
                    Type = NotificationType.FollowRequestReceived,
                    EntityType = NotificationEntityType.User,
                    EntityId = userId.ToString(),
                    Title = "Yeni icma üzvlük sorğusu",
                    Message = $"{community.Name} icmasına qoşulmaq üçün sorğu göndərildi.",
                    ActionUrl = $"/forum/communities/{community.Slug}"
                },
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ForumCommunityLeaveResultDTO> LeaveAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var community = await _context.ForumCommunities
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        var membership = await _context.ForumCommunityMembers
            .FirstOrDefaultAsync(
                x => x.CommunityId == id && x.UserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("İcma üzvlüyü tapılmadı.");

        if (community.OwnerId != userId &&
            membership.Role != ForumCommunityMemberRole.Owner)
        {
            OwnershipAdvanceResult ownershipAdvance = OwnershipAdvanceResult.None;

            _context.ForumCommunityMembers.Remove(membership);
            await RemoveFollowIfExistsAsync(id, userId, cancellationToken);

            if (community.PendingOwnerCandidateId == userId)
            {
                var declinedIds = ReadDeclinedCandidateIds(community);
                declinedIds.Add(userId);
                ownershipAdvance = await AdvanceOwnershipOfferAsync(
                    community,
                    declinedIds,
                    userId,
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await CompleteOwnershipAdvanceAsync(ownershipAdvance, cancellationToken);

            return new ForumCommunityLeaveResultDTO
            {
                LeftCommunity = true
            };
        }

        if (community.PendingOwnerCandidateId.HasValue)
        {
            var pendingCandidateName = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == community.PendingOwnerCandidateId.Value)
                .Select(x => x.UserName)
                .FirstOrDefaultAsync(cancellationToken);

            return new ForumCommunityLeaveResultDTO
            {
                LeftCommunity = false,
                OwnershipOfferCreated = true,
                CandidateUserName = pendingCandidateName
            };
        }

        community.OwnershipTransferStartedAt = DateTime.UtcNow;
        community.OwnershipTransferDeclinedUserIdsJson = "[]";
        community.DeleteIfOwnershipTransferFails = false;

        var candidate = await FindNextOwnershipCandidateAsync(
            id,
            userId,
            [],
            cancellationToken)
            ?? throw new ConflictException(
                "İcmada sahiblik təklif ediləcək başqa aktiv üzv yoxdur. İcmadan ayrılmaq əvəzinə icmanı silə bilərsiniz.");

        community.PendingOwnerCandidateId = candidate.UserId;
        community.UpdatedAt = DateTime.UtcNow;

        await QueueOwnershipOfferAsync(
            community,
            userId,
            candidate.UserId,
            candidate.UserName,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new ForumCommunityLeaveResultDTO
        {
            LeftCommunity = false,
            OwnershipOfferCreated = true,
            CandidateUserName = candidate.UserName
        };
    }

    public async Task FollowAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCommunityCanBeSeenAsync(id, userId, cancellationToken);

        var isActiveMember = await _context.ForumCommunityMembers
            .AsNoTracking()
            .AnyAsync(
                member =>
                    member.CommunityId == id &&
                    member.UserId == userId &&
                    member.Status == ForumCommunityMembershipStatus.Active,
                cancellationToken);

        if (isActiveMember)
        {
            throw new ConflictException(
                "Aktiv icma üzvləri yeni paylaşımlar barədə avtomatik bildiriş alır və icmanı ayrıca izləməli deyil.");
        }

        if (await _context.ForumCommunityFollows.AnyAsync(
                x => x.CommunityId == id && x.UserId == userId,
                cancellationToken))
            return;

        await _context.ForumCommunityFollows.AddAsync(
            new ForumCommunityFollow
            {
                CommunityId = id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnfollowAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var follow = await _context.ForumCommunityFollows
            .FirstOrDefaultAsync(
                x => x.CommunityId == id && x.UserId == userId,
                cancellationToken);

        if (follow is null)
            return;

        _context.ForumCommunityFollows.Remove(follow);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumCommunityMemberDTO>> GetMembershipRequestsAsync(
        int id,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(id, ownerId, cancellationToken);

        return await _context.ForumCommunityMembers
            .AsNoTracking()
            .Where(x =>
                x.CommunityId == id &&
                x.Status == ForumCommunityMembershipStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ForumCommunityMemberDTO
            {
                MembershipId = x.Id,
                UserId = x.UserId,
                UserName = x.User.UserName ?? string.Empty,
                ProfileImageUrl = x.User.ProfileImageUrl,
                Role = x.Role,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task ApproveMembershipAsync(
        int id,
        int membershipId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(id, ownerId, cancellationToken);

        var membership = await _context.ForumCommunityMembers
            .FirstOrDefaultAsync(
                x => x.Id == membershipId &&
                     x.CommunityId == id &&
                     x.Status == ForumCommunityMembershipStatus.Pending,
                cancellationToken)
            ?? throw new NotFoundException("Üzvlük sorğusu tapılmadı.");

        membership.Status = ForumCommunityMembershipStatus.Active;
        membership.UpdatedAt = DateTime.UtcNow;
        await RemoveFollowIfExistsAsync(id, membership.UserId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectMembershipAsync(
        int id,
        int membershipId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(id, ownerId, cancellationToken);

        var membership = await _context.ForumCommunityMembers
            .FirstOrDefaultAsync(
                x => x.Id == membershipId &&
                     x.CommunityId == id &&
                     x.Status == ForumCommunityMembershipStatus.Pending,
                cancellationToken)
            ?? throw new NotFoundException("Üzvlük sorğusu tapılmadı.");

        _context.ForumCommunityMembers.Remove(membership);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumCommunityMemberDTO>> GetMembersAsync(
        int id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerOrModeratorAsync(id, currentUserId, cancellationToken);

        return await _context.ForumCommunityMembers
            .AsNoTracking()
            .Where(x =>
                x.CommunityId == id &&
                x.Status == ForumCommunityMembershipStatus.Active &&
                x.Role != ForumCommunityMemberRole.Owner)
            .OrderBy(x => x.Role == ForumCommunityMemberRole.Moderator ? 0 : 1)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => new ForumCommunityMemberDTO
            {
                MembershipId = x.Id,
                UserId = x.UserId,
                UserName = x.User.UserName ?? string.Empty,
                ProfileImageUrl = x.User.ProfileImageUrl,
                Role = x.Role,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumCommunityInviteCandidateDTO>> SearchInviteCandidatesAsync(
        int id,
        Guid ownerId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var community = await _context.ForumCommunities
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.OwnerId, x.Visibility })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        if (community.OwnerId != ownerId)
            throw new ForbiddenException("Bu əməliyyatı yalnız icma sahibi edə bilər.");
        if (community.Visibility != ForumCommunityVisibility.Private)
            throw new ConflictException("İstifadəçi dəvəti yalnız gizli icmalar üçün mövcuddur.");

        var normalizedQuery = query.NormalizeRequiredText("Axtarış mətni", 100);
        if (normalizedQuery.Length < 2)
            throw new BadRequestException("Axtarış üçün ən azı 2 simvol daxil edin.");

        return await _context.Users
            .AsNoTracking()
            .Where(user =>
                user.Id != ownerId &&
                user.UserName != null &&
                user.UserName.Contains(normalizedQuery))
            .OrderBy(user => user.UserName)
            .Take(20)
            .Select(user => new ForumCommunityInviteCandidateDTO
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                ProfileImageUrl = user.ProfileImageUrl,
                MembershipStatus = _context.ForumCommunityMembers
                    .Where(member =>
                        member.CommunityId == id &&
                        member.UserId == user.Id)
                    .Select(member =>
                        (ForumCommunityMembershipStatus?)member.Status)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(
        int id,
        int membershipId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var currentRole = await EnsureOwnerOrModeratorAsync(
            id,
            currentUserId,
            cancellationToken);

        var community = await _context.ForumCommunities
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        var membership = await _context.ForumCommunityMembers
            .FirstOrDefaultAsync(
                x =>
                    x.Id == membershipId &&
                    x.CommunityId == id &&
                    x.Status == ForumCommunityMembershipStatus.Active,
                cancellationToken)
            ?? throw new NotFoundException("İcma üzvü tapılmadı.");

        if (membership.Role == ForumCommunityMemberRole.Owner ||
            membership.UserId == community.OwnerId)
        {
            throw new ConflictException("İcma sahibi üzvlər siyahısından çıxarıla bilməz.");
        }

        if (currentRole == ForumCommunityMemberRole.Moderator &&
            membership.Role != ForumCommunityMemberRole.Member)
        {
            throw new ForbiddenException(
                "Moderator yalnız adi icma üzvlərini çıxara bilər.");
        }

        _context.ForumCommunityMembers.Remove(membership);
        await RemoveFollowIfExistsAsync(id, membership.UserId, cancellationToken);

        var ownershipAdvance = OwnershipAdvanceResult.None;
        if (community.PendingOwnerCandidateId == membership.UserId)
        {
            var declinedIds = ReadDeclinedCandidateIds(community);
            declinedIds.Add(membership.UserId);
            ownershipAdvance = await AdvanceOwnershipOfferAsync(
                community,
                declinedIds,
                currentUserId,
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await CompleteOwnershipAdvanceAsync(ownershipAdvance, cancellationToken);
    }

    public async Task UpdateMemberRoleAsync(
        int id,
        int membershipId,
        Guid ownerId,
        ForumCommunityMemberRole role,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(id, ownerId, cancellationToken);

        if (role is not ForumCommunityMemberRole.Member and
            not ForumCommunityMemberRole.Moderator)
        {
            throw new BadRequestException(
                "Üzvə yalnız Member və ya Moderator rolu verilə bilər.");
        }

        var membership = await _context.ForumCommunityMembers
            .Include(x => x.User)
            .Include(x => x.Community)
            .FirstOrDefaultAsync(
                x =>
                    x.Id == membershipId &&
                    x.CommunityId == id &&
                    x.Status == ForumCommunityMembershipStatus.Active,
                cancellationToken)
            ?? throw new NotFoundException("İcma üzvü tapılmadı.");

        if (membership.Role == ForumCommunityMemberRole.Owner ||
            membership.UserId == ownerId)
        {
            throw new ConflictException("İcma sahibinin rolu dəyişdirilə bilməz.");
        }

        if (membership.Role == role)
            return;

        membership.Role = role;
        membership.UpdatedAt = DateTime.UtcNow;

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = membership.UserId,
                ActorUserId = ownerId,
                Type = NotificationType.CommunityModeratorAssigned,
                EntityType = NotificationEntityType.None,
                EntityId = id.ToString(),
                Title = role == ForumCommunityMemberRole.Moderator
                    ? "İcma moderatoru təyin edildiniz"
                    : "Moderator rolu geri alındı",
                Message = role == ForumCommunityMemberRole.Moderator
                    ? $"{membership.Community.Name} icmasında moderator səlahiyyəti aldınız."
                    : $"{membership.Community.Name} icmasındakı moderator səlahiyyətiniz geri alındı.",
                ActionUrl = $"/forum/communities/{membership.Community.Slug}",
                BypassPreferences = true
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int id,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var community = await _context.ForumCommunities
            .Include(x => x.Members)
            .Include(x => x.Followers)
            .Include(x => x.Discussions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        if (community.OwnerId != ownerId)
            throw new ForbiddenException("Bu əməliyyatı yalnız icma sahibi edə bilər.");

        var now = DateTime.UtcNow;
        community.IsDeleted = true;
        community.UpdatedAt = now;

        foreach (var member in community.Members)
        {
            member.IsDeleted = true;
            member.UpdatedAt = now;
        }

        foreach (var discussion in community.Discussions)
        {
            discussion.IsDeleted = true;
            discussion.UpdatedAt = now;
        }

        _context.ForumCommunityFollows.RemoveRange(community.Followers);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task InviteAsync(
        int id,
        Guid ownerId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var community = await _context.ForumCommunities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        if (community.OwnerId != ownerId)
            throw new ForbiddenException("Yalnız icma sahibi dəvət göndərə bilər.");
        if (community.Visibility != ForumCommunityVisibility.Private)
            throw new ConflictException("Dəvət yalnız gizli icmalar üçün tələb olunur.");

        var normalizedUserName = userName.NormalizeRequiredText("İstifadəçi adı", 256);
        var invitedUser = await _context.Users
            .AsNoTracking()
            .Where(x => x.NormalizedUserName == normalizedUserName.ToUpperInvariant())
            .Select(x => new { x.Id, UserName = x.UserName ?? string.Empty })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        if (invitedUser.Id == ownerId)
            throw new ConflictException("İcma sahibi artıq icmanın üzvüdür.");

        var membership = await _context.ForumCommunityMembers
            .FirstOrDefaultAsync(
                x => x.CommunityId == id && x.UserId == invitedUser.Id,
                cancellationToken);

        if (membership?.Status == ForumCommunityMembershipStatus.Active)
            throw new ConflictException("İstifadəçi artıq icmanın üzvüdür.");

        if (membership is null)
        {
            await _context.ForumCommunityMembers.AddAsync(
                new ForumCommunityMember
                {
                    CommunityId = id,
                    UserId = invitedUser.Id,
                    Role = ForumCommunityMemberRole.Member,
                    Status = ForumCommunityMembershipStatus.Invited,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken);
        }
        else
        {
            membership.Status = ForumCommunityMembershipStatus.Invited;
            membership.IsDeleted = false;
            membership.UpdatedAt = DateTime.UtcNow;
        }

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = invitedUser.Id,
                ActorUserId = ownerId,
                Type = NotificationType.FollowRequestReceived,
                EntityType = NotificationEntityType.User,
                EntityId = ownerId.ToString(),
                Title = "Gizli icmaya dəvət",
                Message = $"{community.Name} icmasına dəvət aldınız.",
                ActionUrl = $"/forum/communities/{community.Slug}"
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AcceptInviteAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _context.ForumCommunityMembers
            .FirstOrDefaultAsync(
                x => x.CommunityId == id &&
                     x.UserId == userId &&
                     x.Status == ForumCommunityMembershipStatus.Invited,
                cancellationToken)
            ?? throw new NotFoundException("Aktiv icma dəvəti tapılmadı.");

        membership.Status = ForumCommunityMembershipStatus.Active;
        membership.UpdatedAt = DateTime.UtcNow;
        await RemoveFollowIfExistsAsync(id, userId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AcceptOwnershipOfferAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        var community = await _context.ForumCommunities
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        if (community.PendingOwnerCandidateId != userId)
            throw new NotFoundException("Aktiv sahiblik təklifi tapılmadı.");

        var currentOwnerMembership = await _context.ForumCommunityMembers
            .FirstOrDefaultAsync(
                x =>
                    x.CommunityId == id &&
                    x.UserId == community.OwnerId &&
                    x.Status == ForumCommunityMembershipStatus.Active,
                cancellationToken)
            ?? throw new ConflictException("Hazırkı icma sahibinin üzvlüyü tapılmadı.");

        var candidateMembership = await _context.ForumCommunityMembers
            .FirstOrDefaultAsync(
                x =>
                    x.CommunityId == id &&
                    x.UserId == userId &&
                    x.Status == ForumCommunityMembershipStatus.Active,
                cancellationToken)
            ?? throw new ConflictException("Sahiblik yalnız aktiv icma üzvünə verilə bilər.");

        var previousOwnerId = community.OwnerId;
        community.OwnerId = userId;
        community.UpdatedAt = DateTime.UtcNow;
        ClearOwnershipTransfer(community);

        candidateMembership.Role = ForumCommunityMemberRole.Owner;
        candidateMembership.UpdatedAt = DateTime.UtcNow;

        _context.ForumCommunityMembers.Remove(currentOwnerMembership);
        await RemoveFollowIfExistsAsync(id, previousOwnerId, cancellationToken);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = previousOwnerId,
                ActorUserId = userId,
                Type = NotificationType.CommunityOwnershipOffered,
                Priority = NotificationPriority.High,
                EntityType = NotificationEntityType.None,
                EntityId = id.ToString(),
                Title = "İcma sahibliyi ötürüldü",
                Message = $"{community.Name} icmasının sahiblik təklifi qəbul edildi və siz icmadan ayrıldınız.",
                ActionUrl = "/forum",
                BypassPreferences = true
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await _accountDeletionService.TryFinalizeAsync(
            previousOwnerId,
            cancellationToken);
    }

    public async Task DeclineOwnershipOfferAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var community = await _context.ForumCommunities
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        if (community.PendingOwnerCandidateId != userId)
            throw new NotFoundException("Aktiv sahiblik təklifi tapılmadı.");

        var declinedIds = ReadDeclinedCandidateIds(community);
        declinedIds.Add(userId);

        var ownershipAdvance = await AdvanceOwnershipOfferAsync(
            community,
            declinedIds,
            userId,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await CompleteOwnershipAdvanceAsync(ownershipAdvance, cancellationToken);
    }

    private async Task RemoveFollowIfExistsAsync(
        int communityId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var follow = await _context.ForumCommunityFollows
            .FirstOrDefaultAsync(
                item => item.CommunityId == communityId && item.UserId == userId,
                cancellationToken);

        if (follow is not null)
            _context.ForumCommunityFollows.Remove(follow);
    }

    private async Task<ForumCommunityMemberRole> EnsureOwnerOrModeratorAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var role = await _context.ForumCommunityMembers
            .AsNoTracking()
            .Where(x =>
                x.CommunityId == id &&
                x.UserId == userId &&
                x.Status == ForumCommunityMembershipStatus.Active)
            .Select(x => (ForumCommunityMemberRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("İcmanı idarə etmək üçün icazəniz yoxdur.");

        if (role is not ForumCommunityMemberRole.Owner and
            not ForumCommunityMemberRole.Moderator)
        {
            throw new ForbiddenException("İcmanı idarə etmək üçün icazəniz yoxdur.");
        }

        return role;
    }

    private async Task<OwnershipCandidate?> FindNextOwnershipCandidateAsync(
        int communityId,
        Guid ownerId,
        IReadOnlyCollection<Guid> excludedUserIds,
        CancellationToken cancellationToken)
    {
        var query = _context.ForumCommunityMembers
            .AsNoTracking()
            .Where(x =>
                x.CommunityId == communityId &&
                x.UserId != ownerId &&
                x.Status == ForumCommunityMembershipStatus.Active &&
                !x.IsDeleted &&
                x.User.AccountDeletionRequestedAtUtc == null);

        if (excludedUserIds.Count > 0)
            query = query.Where(x => !excludedUserIds.Contains(x.UserId));

        return await query
            .OrderBy(x => x.Role == ForumCommunityMemberRole.Moderator ? 0 : 1)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => new OwnershipCandidate(
                x.UserId,
                x.User.UserName ?? string.Empty))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<OwnershipAdvanceResult> AdvanceOwnershipOfferAsync(
        ForumCommunity community,
        HashSet<Guid> declinedIds,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        community.OwnershipTransferDeclinedUserIdsJson = JsonSerializer.Serialize(declinedIds);

        var nextCandidate = await FindNextOwnershipCandidateAsync(
            community.Id,
            community.OwnerId,
            declinedIds,
            cancellationToken);

        if (nextCandidate is null)
        {
            if (community.DeleteIfOwnershipTransferFails)
            {
                return new OwnershipAdvanceResult(
                    community.Id,
                    community.OwnerId);
            }

            ClearOwnershipTransfer(community);
            community.UpdatedAt = DateTime.UtcNow;

            await _notificationService.QueueAsync(
                new NotificationCreateRequest
                {
                    RecipientUserId = community.OwnerId,
                    ActorUserId = actorUserId,
                    Type = NotificationType.CommunityOwnershipOffered,
                    EntityType = NotificationEntityType.None,
                    EntityId = community.Id.ToString(),
                    Title = "Sahiblik ötürülmədi",
                    Message = $"{community.Name} icmasında sahiblik təklifini qəbul edən üzv tapılmadı. Siz hələ də icma sahibisiniz.",
                    ActionUrl = $"/forum/communities/{community.Slug}",
                    BypassPreferences = true
                },
                cancellationToken);

            return OwnershipAdvanceResult.None;
        }

        community.PendingOwnerCandidateId = nextCandidate.UserId;
        community.UpdatedAt = DateTime.UtcNow;

        await QueueOwnershipOfferAsync(
            community,
            community.OwnerId,
            nextCandidate.UserId,
            nextCandidate.UserName,
            cancellationToken);

        return OwnershipAdvanceResult.None;
    }

    private async Task CompleteOwnershipAdvanceAsync(
        OwnershipAdvanceResult result,
        CancellationToken cancellationToken)
    {
        if (!result.CommunityToDeleteId.HasValue)
            return;

        await _accountDeletionService.HardDeleteCommunityAsync(
            result.CommunityToDeleteId.Value,
            cancellationToken);

        if (result.OwnerToFinalizeId.HasValue)
        {
            await _accountDeletionService.TryFinalizeAsync(
                result.OwnerToFinalizeId.Value,
                cancellationToken);
        }
    }

    private async Task QueueOwnershipOfferAsync(
        ForumCommunity community,
        Guid ownerId,
        Guid candidateUserId,
        string candidateUserName,
        CancellationToken cancellationToken)
    {
        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = candidateUserId,
                ActorUserId = ownerId,
                Type = NotificationType.CommunityOwnershipOffered,
                Priority = NotificationPriority.High,
                EntityType = NotificationEntityType.None,
                EntityId = community.Id.ToString(),
                Title = "İcma sahibliyi təklifi",
                Message = $"{community.Name} icmasının sahibi olmaq üçün təklif aldınız.",
                ActionUrl = $"/forum/communities/{community.Slug}",
                DataJson = JsonSerializer.Serialize(new
                {
                    communityId = community.Id,
                    communitySlug = community.Slug,
                    candidateUserName
                }),
                BypassPreferences = true
            },
            cancellationToken);
    }

    private static HashSet<Guid> ReadDeclinedCandidateIds(
        ForumCommunity community)
    {
        if (string.IsNullOrWhiteSpace(community.OwnershipTransferDeclinedUserIdsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<HashSet<Guid>>(
                    community.OwnershipTransferDeclinedUserIdsJson)
                ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void ClearOwnershipTransfer(ForumCommunity community)
    {
        community.PendingOwnerCandidateId = null;
        community.OwnershipTransferStartedAt = null;
        community.OwnershipTransferDeclinedUserIdsJson = null;
        community.DeleteIfOwnershipTransferFails = false;
    }

    private sealed record OwnershipCandidate(
        Guid UserId,
        string UserName);

    private sealed record OwnershipAdvanceResult(
        int? CommunityToDeleteId,
        Guid? OwnerToFinalizeId)
    {
        public static OwnershipAdvanceResult None { get; } = new(null, null);
    }

    private async Task EnsureOwnerAsync(
        int id,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var actualOwnerId = await _context.ForumCommunities
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (Guid?)x.OwnerId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");

        if (actualOwnerId != ownerId)
            throw new ForbiddenException("Bu əməliyyatı yalnız icma sahibi edə bilər.");
    }

    private async Task EnsureCommunityCanBeSeenAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var canSee = await _context.ForumCommunities
            .AsNoTracking()
            .AnyAsync(community =>
                community.Id == id &&
                (community.Visibility != ForumCommunityVisibility.Private ||
                 community.Members.Any(member =>
                     member.UserId == userId &&
                     member.Status == ForumCommunityMembershipStatus.Active)),
                cancellationToken);

        if (!canSee)
            throw new NotFoundException("İcma tapılmadı.");
    }

    private async Task<string> CreateUniqueSlugAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var baseSlug = SlugHelper.Generate(name, "community");
        var slug = baseSlug;
        var suffix = 2;

        while (await _context.ForumCommunities
                   .IgnoreQueryFilters()
                   .AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private static IQueryable<ForumCommunitySummaryDTO> Project(
        IQueryable<ForumCommunity> query,
        Guid? currentUserId)
    {
        return query.Select(x => new ForumCommunitySummaryDTO
        {
            Id = x.Id,
            Name = x.Name,
            Slug = x.Slug,
            Description = x.Description,
            IconUrl = x.IconUrl,
            CoverImageUrl = x.CoverImageUrl,
            Category = x.Category,
            Visibility = x.Visibility,
            OwnerId = x.OwnerId,
            OwnerUserName = x.Owner.UserName ?? string.Empty,
            MemberCount = x.Members.Count(member =>
                member.Status == ForumCommunityMembershipStatus.Active),
            FollowerCount = x.Followers.Count(follow =>
                !x.Members.Any(member =>
                    member.UserId == follow.UserId &&
                    member.Status == ForumCommunityMembershipStatus.Active)),
            DiscussionCount = x.Discussions.Count,
            CurrentUserMembershipStatus = currentUserId.HasValue
                ? x.Members
                    .Where(member => member.UserId == currentUserId.Value)
                    .Select(member => (ForumCommunityMembershipStatus?)member.Status)
                    .FirstOrDefault()
                : null,
            CurrentUserRole = currentUserId.HasValue
                ? x.Members
                    .Where(member =>
                        member.UserId == currentUserId.Value &&
                        member.Status == ForumCommunityMembershipStatus.Active)
                    .Select(member => (ForumCommunityMemberRole?)member.Role)
                    .FirstOrDefault()
                : null,
            IsFollowedByCurrentUser = currentUserId.HasValue &&
                !x.Members.Any(member =>
                    member.UserId == currentUserId.Value &&
                    member.Status == ForumCommunityMembershipStatus.Active) &&
                x.Followers.Any(follow => follow.UserId == currentUserId.Value),
            CanManage = currentUserId.HasValue && x.OwnerId == currentUserId.Value,
            CanModerate = currentUserId.HasValue &&
                x.Members.Any(member =>
                    member.UserId == currentUserId.Value &&
                    member.Status == ForumCommunityMembershipStatus.Active &&
                    (member.Role == ForumCommunityMemberRole.Owner ||
                     member.Role == ForumCommunityMemberRole.Moderator)),
            HasOwnershipOfferForCurrentUser = currentUserId.HasValue &&
                x.PendingOwnerCandidateId == currentUserId.Value,
            HasPendingOwnershipTransfer = x.PendingOwnerCandidateId.HasValue,
            PendingOwnerCandidateUserName = currentUserId.HasValue &&
                x.OwnerId == currentUserId.Value &&
                x.PendingOwnerCandidateId.HasValue
                    ? x.Members
                        .Where(member => member.UserId == x.PendingOwnerCandidateId.Value)
                        .Select(member => member.User.UserName)
                        .FirstOrDefault()
                    : null,
            CreatedAt = x.CreatedAt
        });
    }

}
