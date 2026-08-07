using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public sealed class GameListService : IGameListService
    {
        private const int MaxNameLength = 100;
        private const int MaxDescriptionLength = 1000;

        private readonly AppDbContext _context;
        private readonly IGameService _gameService;
        private readonly INotificationService _notificationService;

        public GameListService(
            AppDbContext context,
            IGameService gameService,
            INotificationService notificationService)
        {
            _context = context;
            _gameService = gameService;
            _notificationService = notificationService;
        }

        public async Task<PagedResultDTO<GameListSummaryDTO>> GetUserListsAsync(
            Guid userId,
            Guid? currentUserId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default)
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId, cancellationToken);

            if (!userExists)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            await CheckBlockAccessAsync(userId, currentUserId, cancellationToken);

            var query = _context.GameLists
                .AsNoTracking()
                .Where(x => x.UserId == userId);

            if (currentUserId != userId)
            {
                var isFollowing = currentUserId.HasValue &&
                                  await _context.UserFollows
                                      .AsNoTracking()
                                      .AnyAsync(x =>
                                          x.FollowerId == currentUserId.Value &&
                                          x.FollowingId == userId &&
                                          x.Status == FollowStatus.Accepted,
                                          cancellationToken);

                query = isFollowing
                    ? query.Where(x => x.Visibility != ListVisibility.Private)
                    : query.Where(x => x.Visibility == ListVisibility.Public);
            }

            var projectedQuery = query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => new GameListSummaryDTO
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    Visibility = x.Visibility,
                    IsRanked = x.IsRanked,
                    OwnerId = x.UserId,
                    OwnerUserName = x.User.UserName ?? string.Empty,
                    GameCount = x.GameListItems.Count,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    LikeCount = x.Likes.Count,
                    CommentCount = x.Comments.Count,
                    IsLikedByCurrentUser = currentUserId.HasValue &&
                        x.Likes.Any(like => like.UserId == currentUserId.Value),
                    BackgroundCoverImageUrl = x.IsRanked
                        ? x.GameListItems
                            .OrderBy(item => item.Position ?? int.MaxValue)
                            .ThenBy(item => item.AddedAt)
                            .Select(item => item.CoverImageUrl)
                            .FirstOrDefault()
                        : x.GameListItems
                            .OrderBy(item => item.AddedAt)
                            .Select(item => item.CoverImageUrl)
                            .FirstOrDefault(),
                    CollaboratorCount = x.Collaborators.Count(collaborator =>
                        collaborator.Status == GameListCollaborationStatus.Accepted)
                });

            return await projectedQuery.ToPagedResultAsync(
                pagination,
                cancellationToken);
        }

        public async Task<PagedResultDTO<GameListSummaryDTO>> GetCollaboratingListsAsync(
            Guid userId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default)
        {
            var query = _context.GameListCollaborators
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.Status == GameListCollaborationStatus.Pending)
                .ThenByDescending(x => x.InvitedAtUtc)
                .Select(x => new GameListSummaryDTO
                {
                    Id = x.GameList.Id,
                    Name = x.GameList.Name,
                    Description = x.GameList.Description,
                    Visibility = x.GameList.Visibility,
                    IsRanked = x.GameList.IsRanked,
                    OwnerId = x.GameList.UserId,
                    OwnerUserName = x.GameList.User.UserName ?? string.Empty,
                    GameCount = x.GameList.GameListItems.Count,
                    CreatedAt = x.GameList.CreatedAt,
                    UpdatedAt = x.GameList.UpdatedAt,
                    LikeCount = x.GameList.Likes.Count,
                    CommentCount = x.GameList.Comments.Count,
                    IsLikedByCurrentUser = x.GameList.Likes.Any(like => like.UserId == userId),
                    BackgroundCoverImageUrl = x.GameList.IsRanked
                        ? x.GameList.GameListItems
                            .OrderBy(item => item.Position ?? int.MaxValue)
                            .ThenBy(item => item.AddedAt)
                            .Select(item => item.CoverImageUrl)
                            .FirstOrDefault()
                        : x.GameList.GameListItems
                            .OrderBy(item => item.AddedAt)
                            .Select(item => item.CoverImageUrl)
                            .FirstOrDefault(),
                    CollaboratorCount = x.GameList.Collaborators.Count(collaborator =>
                        collaborator.Status == GameListCollaborationStatus.Accepted),
                    CurrentUserCollaborationStatus = x.Status
                });

            return await query.ToPagedResultAsync(pagination, cancellationToken);
        }

        public async Task<GameListDetailDTO> GetByIdAsync(
            int id,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            id.EnsureValidId();

            var gameList = await _context.GameLists
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Description,
                    x.Visibility,
                    x.IsRanked,
                    OwnerId = x.UserId,
                    OwnerUserName = x.User.UserName ?? string.Empty,
                    OwnerProfileImageUrl = x.User.ProfileImageUrl,
                    GameCount = x.GameListItems.Count,
                    x.CreatedAt,
                    x.UpdatedAt,
                    LikeCount = x.Likes.Count,
                    CommentCount = x.Comments.Count,
                    IsLikedByCurrentUser = currentUserId.HasValue &&
                        x.Likes.Any(like => like.UserId == currentUserId.Value)
                })
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

            var currentCollaborationStatus = currentUserId.HasValue
                ? await _context.GameListCollaborators
                    .AsNoTracking()
                    .Where(x => x.GameListId == id && x.UserId == currentUserId.Value)
                    .Select(x => (GameListCollaborationStatus?)x.Status)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            await CheckListAccessAsync(
                gameList.OwnerId,
                gameList.Visibility,
                currentUserId,
                currentCollaborationStatus,
                cancellationToken);

            var currentUserGames = currentUserId.HasValue
                ? _context.UserGames
                    .AsNoTracking()
                    .Where(x => x.UserId == currentUserId.Value)
                : _context.UserGames
                    .AsNoTracking()
                    .Where(_ => false);

            var gameEntries =
                from item in _context.GameListItems.AsNoTracking()
                where item.GameListId == id
                join userGame in currentUserGames
                    on new
                    {
                        Source = item.GameSource,
                        GameId = item.GameSourceId
                    }
                    equals new
                    {
                        Source = userGame.Source,
                        GameId = userGame.GameId
                    }
                    into userGameGroup
                from userGame in userGameGroup.DefaultIfEmpty()
                select new GameListEntryDTO
                {
                    Source = item.GameSource,
                    Id = item.GameSourceId,
                    Title = item.GameTitle,
                    CoverImageUrl = item.CoverImageUrl,
                    ReleaseDate = item.ReleaseDate,
                    AverageRating = item.Rating,
                    CurrentUserStatus = userGame == null ? null : userGame.Status,
                    CurrentUserRating = userGame == null ? null : userGame.Rating,
                    Position = item.Position,
                    AddedAt = item.AddedAt
                };

            var games = gameList.IsRanked
                ? await gameEntries
                    .OrderBy(x => x.Position ?? int.MaxValue)
                    .ThenBy(x => x.AddedAt)
                    .ToListAsync(cancellationToken)
                : await gameEntries
                    .OrderBy(x => x.AddedAt)
                    .ThenBy(x => x.Title)
                    .ToListAsync(cancellationToken);

            var currentUserPlayedGameCount =
                currentUserId.HasValue && gameList.GameCount > 0
                    ? await CountPlayedGamesAsync(
                        id,
                        currentUserId.Value,
                        cancellationToken)
                    : 0;

            var collaborators = await _context.GameListCollaborators
                .AsNoTracking()
                .Where(x => x.GameListId == id)
                .OrderBy(x => x.Status)
                .ThenBy(x => x.InvitedAtUtc)
                .Select(x => new GameListCollaboratorDTO
                {
                    UserId = x.UserId,
                    UserName = x.User.UserName ?? string.Empty,
                    ProfileImageUrl = x.User.ProfileImageUrl,
                    Status = x.Status,
                    InvitedAtUtc = x.InvitedAtUtc,
                    RespondedAtUtc = x.RespondedAtUtc
                })
                .ToListAsync(cancellationToken);

            var backgroundCoverImageUrl = gameList.IsRanked
                ? games.OrderBy(x => x.Position ?? int.MaxValue).ThenBy(x => x.AddedAt)
                    .Select(x => x.CoverImageUrl).FirstOrDefault()
                : games.OrderBy(x => x.AddedAt)
                    .Select(x => x.CoverImageUrl).FirstOrDefault();

            return new GameListDetailDTO
            {
                Id = gameList.Id,
                Name = gameList.Name,
                Description = gameList.Description,
                Visibility = gameList.Visibility,
                IsRanked = gameList.IsRanked,
                OwnerId = gameList.OwnerId,
                OwnerUserName = gameList.OwnerUserName,
                OwnerProfileImageUrl = gameList.OwnerProfileImageUrl,
                IsOwner = currentUserId == gameList.OwnerId,
                CanEditGames = currentUserId == gameList.OwnerId ||
                    currentCollaborationStatus == GameListCollaborationStatus.Accepted,
                CanManageCollaborators = currentUserId == gameList.OwnerId,
                CurrentUserCollaborationStatus = currentCollaborationStatus,
                GameCount = gameList.GameCount,
                CurrentUserPlayedGameCount = currentUserPlayedGameCount,
                CreatedAt = gameList.CreatedAt,
                UpdatedAt = gameList.UpdatedAt,
                LikeCount = gameList.LikeCount,
                CommentCount = gameList.CommentCount,
                IsLikedByCurrentUser = gameList.IsLikedByCurrentUser,
                BackgroundCoverImageUrl = backgroundCoverImageUrl,
                Collaborators = collaborators,
                Games = games
            };
        }

        public async Task<GameListPlayedProgressDTO> GetPlayedProgressAsync(
            int id,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            // GetByIdAsync performs the same visibility, follow, collaboration and block checks
            // as the list detail endpoint. Its played count is based only on UserGame.Status.
            var detail = await GetByIdAsync(id, currentUserId, cancellationToken);

            return new GameListPlayedProgressDTO
            {
                PlayedGameCount = detail.CurrentUserPlayedGameCount,
                TotalGameCount = detail.GameCount
            };
        }

        public async Task<int> CreateAsync(
            Guid userId,
            GameListCreateDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            dto.Visibility.EnsureDefined("Siyahının görünmə tipi");

            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId, cancellationToken);

            if (!userExists)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            var name = dto.Name.NormalizeName(MaxNameLength, "Siyahı");
            await EnsureListNameIsAvailableAsync(userId, name, null, cancellationToken);

            var gameList = new GameList
            {
                Name = name,
                Description = dto.Description.NormalizeOptionalText(
                    "Siyahının açıqlaması",
                    MaxDescriptionLength),
                Visibility = dto.Visibility,
                IsRanked = dto.IsRanked,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            await _context.GameLists.AddAsync(gameList, cancellationToken);
            await SaveListChangesAsync(cancellationToken);

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.GameListCreated,
                gameListId: gameList.Id,
                subjectTitle: gameList.Name);

            if (gameList.Visibility == ListVisibility.Public)
            {
                await _notificationService.QueueForFollowersAsync(
                    new FollowerNotificationRequest
                    {
                        ActorUserId = userId,
                        Type = NotificationType.FollowingPublicListCreated,
                        EntityType = NotificationEntityType.GameList,
                        EntityId = gameList.Id.ToString(),
                        Title = "Yeni açıq siyahı",
                        Message = $"{gameList.Name} adlı yeni açıq siyahı yaratdı.",
                        ActionUrl = $"/lists/{gameList.Id}"
                    },
                    cancellationToken);
            }

            await SaveListChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return gameList.Id;
        }

        public async Task UpdateAsync(
            int id,
            Guid userId,
            GameListUpdateDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var gameList = await GetUserGameListAsync(
                id,
                userId,
                includeItems: true,
                cancellationToken);

            var hasChanges = false;

            if (dto.Name is not null)
            {
                var name = dto.Name.NormalizeName(MaxNameLength, "Siyahı");
                await EnsureListNameIsAvailableAsync(userId, name, id, cancellationToken);
                gameList.Name = name;
                hasChanges = true;
            }

            var description = dto.Description.NormalizeOptionalText(
                "Siyahının açıqlaması",
                MaxDescriptionLength);

            if (description != gameList.Description)
            {
                gameList.Description = description;
                hasChanges = true;
            }

            if (dto.Visibility.HasValue)
            {
                dto.Visibility.Value.EnsureDefined("Siyahının görünmə tipi");
                gameList.Visibility = dto.Visibility.Value;
                hasChanges = true;
            }

            if (dto.IsRanked.HasValue && dto.IsRanked.Value != gameList.IsRanked)
            {
                gameList.IsRanked = dto.IsRanked.Value;

                if (gameList.IsRanked)
                {
                    var position = 1;

                    foreach (var item in gameList.GameListItems
                                 .OrderBy(x => x.AddedAt)
                                 .ThenBy(x => x.GameSource)
                                 .ThenBy(x => x.GameSourceId))
                    {
                        item.Position = position++;
                    }
                }
                else
                {
                    foreach (var item in gameList.GameListItems)
                        item.Position = null;
                }

                hasChanges = true;
            }

            if (!hasChanges)
                return;

            gameList.UpdatedAt = DateTime.UtcNow;

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.GameListUpdated,
                gameListId: gameList.Id,
                subjectTitle: gameList.Name);

            await SaveListChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(
            int id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var gameList = await GetUserGameListAsync(
                id,
                userId,
                includeItems: false,
                cancellationToken);

            gameList.IsDeleted = true;
            gameList.UpdatedAt = DateTime.UtcNow;

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.GameListDeleted,
                gameListId: gameList.Id,
                subjectTitle: gameList.Name);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task RestoreAsync(
            int id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            id.EnsureValidId();

            var gameList = await _context.GameLists
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
                ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

            if (!gameList.IsDeleted)
                return;

            await EnsureListNameIsAvailableAsync(
                userId,
                gameList.Name,
                gameList.Id,
                cancellationToken);

            gameList.IsDeleted = false;
            gameList.UpdatedAt = DateTime.UtcNow;
            await SaveListChangesAsync(cancellationToken);
        }

        public async Task AddGameAsync(
            int listId,
            CatalogSource source,
            long gameId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            gameId.EnsureValidCatalogReference(source, "Oyun");

            var gameList = await GetEditableGameListAsync(
                listId,
                userId,
                cancellationToken);

            var itemExists = await _context.GameListItems
                .AsNoTracking()
                .AnyAsync(x =>
                    x.GameListId == listId &&
                    x.GameSource == source &&
                    x.GameSourceId == gameId,
                    cancellationToken);

            if (itemExists)
                throw new ConflictException("Bu oyun siyahıda artıq mövcuddur.");

            var game = await _gameService.GetByIdAsync(source, gameId, cancellationToken);

            int? position = null;

            if (gameList.IsRanked)
            {
                position = (await _context.GameListItems
                    .Where(x => x.GameListId == listId)
                    .MaxAsync(x => (int?)x.Position, cancellationToken) ?? 0) + 1;
            }

            var gameListItem = new GameListItem
            {
                GameListId = listId,
                GameSource = source,
                GameSourceId = gameId,
                GameTitle = game.Title,
                CoverImageUrl = game.CoverImageUrl,
                ReleaseDate = game.ReleaseDate,
                Rating = game.AverageRating,
                Position = position,
                AddedAt = DateTime.UtcNow
            };

            await _context.GameListItems.AddAsync(gameListItem, cancellationToken);
            gameList.UpdatedAt = DateTime.UtcNow;

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.GameAddedToList,
                gameSource: source,
                gameId: gameId,
                gameListId: gameList.Id,
                subjectTitle: gameList.Name);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException("Bu oyun siyahıda artıq mövcuddur.");
            }
        }

        public async Task RemoveGameAsync(
            int listId,
            CatalogSource source,
            long gameId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            gameId.EnsureValidCatalogReference(source, "Oyun");

            var gameList = await GetEditableGameListAsync(
                listId,
                userId,
                cancellationToken);

            var item = await _context.GameListItems
                .FirstOrDefaultAsync(x =>
                    x.GameListId == listId &&
                    x.GameSource == source &&
                    x.GameSourceId == gameId,
                    cancellationToken)
                ?? throw new NotFoundException("Oyun bu siyahıda tapılmadı.");

            var removedPosition = item.Position;
            _context.GameListItems.Remove(item);

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            if (gameList.IsRanked && removedPosition.HasValue)
            {
                await _context.GameListItems
                    .Where(x =>
                        x.GameListId == listId &&
                        x.Position > removedPosition.Value)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            x => x.Position,
                            x => x.Position - 1),
                        cancellationToken);
            }

            gameList.UpdatedAt = DateTime.UtcNow;

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.GameRemovedFromList,
                gameSource: source,
                gameId: gameId,
                gameListId: gameList.Id,
                subjectTitle: gameList.Name);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task UpdateGamePositionAsync(
            int listId,
            CatalogSource source,
            long gameId,
            int position,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            gameId.EnsureValidCatalogReference(source, "Oyun");

            var gameList = await GetEditableGameListAsync(
                listId,
                userId,
                cancellationToken);

            if (!gameList.IsRanked)
                throw new BadRequestException("Yalnız sıralanmış siyahılarda oyunların mövqeyi dəyişdirilə bilər.");

            var itemCount = await _context.GameListItems
                .AsNoTracking()
                .CountAsync(x => x.GameListId == listId, cancellationToken);

            if (position < 1 || position > itemCount)
                throw new BadRequestException($"Mövqe 1-{itemCount} aralığında olmalıdır.");

            var item = await _context.GameListItems
                .FirstOrDefaultAsync(
                    x => x.GameListId == listId &&
                         x.GameSource == source &&
                         x.GameSourceId == gameId,
                    cancellationToken)
                ?? throw new NotFoundException("Oyun bu siyahıda tapılmadı.");

            var oldPosition = item.Position
                ?? throw new BadRequestException("Oyunun mövqeyi müəyyən edilməyib.");

            if (oldPosition == position)
                return;

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            if (position < oldPosition)
            {
                await _context.GameListItems
                    .Where(x =>
                        x.GameListId == listId &&
                        x.Position >= position &&
                        x.Position < oldPosition)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            x => x.Position,
                            x => x.Position + 1),
                        cancellationToken);
            }
            else
            {
                await _context.GameListItems
                    .Where(x =>
                        x.GameListId == listId &&
                        x.Position > oldPosition &&
                        x.Position <= position)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            x => x.Position,
                            x => x.Position - 1),
                        cancellationToken);
            }

            item.Position = position;
            gameList.UpdatedAt = DateTime.UtcNow;

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.GameListGameReordered,
                gameSource: source,
                gameId: gameId,
                gameListId: gameList.Id,
                subjectTitle: gameList.Name);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }


        public async Task ReorderGamesAsync(
            int listId,
            Guid userId,
            GameListReorderDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            var gameList = await GetEditableGameListAsync(listId, userId, cancellationToken);

            if (!gameList.IsRanked)
                throw new BadRequestException("Yalnız ranklanmış siyahılarda oyunların sırası dəyişdirilə bilər.");

            var items = await _context.GameListItems
                .Where(x => x.GameListId == listId)
                .ToListAsync(cancellationToken);

            if (dto.Items.Count != items.Count)
                throw new BadRequestException("Göndərilən sıralama siyahıdakı bütün oyunları əhatə etməlidir.");

            var requestedKeys = dto.Items
                .Select(x => (x.Source, x.GameId))
                .ToList();

            if (requestedKeys.Distinct().Count() != requestedKeys.Count)
                throw new BadRequestException("Sıralamada eyni oyun bir neçə dəfə göndərilib.");

            var itemMap = items.ToDictionary(x => (x.GameSource, x.GameSourceId));
            if (requestedKeys.Any(key => !itemMap.ContainsKey(key)))
                throw new BadRequestException("Sıralamada siyahıya aid olmayan oyun var.");

            for (var index = 0; index < requestedKeys.Count; index++)
                itemMap[requestedKeys[index]].Position = index + 1;

            gameList.UpdatedAt = DateTime.UtcNow;
            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.GameListGameReordered,
                gameListId: gameList.Id,
                subjectTitle: gameList.Name);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> CloneAsync(
            int sourceListId,
            Guid userId,
            GameListCloneDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            dto.Visibility.EnsureDefined("Siyahının görünmə tipi");

            var source = await _context.GameLists
                .AsNoTracking()
                .Include(x => x.GameListItems)
                .FirstOrDefaultAsync(x => x.Id == sourceListId, cancellationToken)
                ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

            var collaborationStatus = await _context.GameListCollaborators
                .AsNoTracking()
                .Where(x => x.GameListId == sourceListId && x.UserId == userId)
                .Select(x => (GameListCollaborationStatus?)x.Status)
                .FirstOrDefaultAsync(cancellationToken);

            await CheckListAccessAsync(
                source.UserId,
                source.Visibility,
                userId,
                collaborationStatus,
                cancellationToken);

            if (source.UserId != userId &&
                collaborationStatus == GameListCollaborationStatus.Pending)
            {
                throw new ForbiddenException(
                    "Siyahını klonlamaq üçün əməkdaşlıq dəvətini əvvəlcə qəbul edin.");
            }

            var name = dto.Name.NormalizeName(MaxNameLength, "Siyahı");
            await EnsureListNameIsAvailableAsync(userId, name, null, cancellationToken);

            var clone = new GameList
            {
                Name = name,
                Description = source.Description,
                Visibility = dto.Visibility,
                IsRanked = source.IsRanked,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var sourceItem in source.GameListItems
                         .OrderBy(x => source.IsRanked ? x.Position ?? int.MaxValue : 0)
                         .ThenBy(x => x.AddedAt))
            {
                clone.GameListItems.Add(new GameListItem
                {
                    GameSource = sourceItem.GameSource,
                    GameSourceId = sourceItem.GameSourceId,
                    GameTitle = sourceItem.GameTitle,
                    CoverImageUrl = sourceItem.CoverImageUrl,
                    ReleaseDate = sourceItem.ReleaseDate,
                    Rating = sourceItem.Rating,
                    Position = source.IsRanked ? sourceItem.Position : null,
                    AddedAt = sourceItem.AddedAt
                });
            }

            await _context.GameLists.AddAsync(clone, cancellationToken);
            await SaveListChangesAsync(cancellationToken);

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.GameListCreated,
                gameListId: clone.Id,
                subjectTitle: clone.Name);
            await _context.SaveChangesAsync(cancellationToken);

            return clone.Id;
        }

        public async Task InviteCollaboratorAsync(
            int listId,
            Guid ownerUserId,
            GameListCollaboratorInviteDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            var gameList = await GetUserGameListAsync(listId, ownerUserId, false, cancellationToken);
            var userName = dto.UserName?.Trim();

            if (string.IsNullOrWhiteSpace(userName))
                throw new BadRequestException("Əməkdaşın istifadəçi adını daxil edin.");

            var invitedUser = await _context.Users
                .AsNoTracking()
                .Where(x => x.NormalizedUserName == userName.ToUpperInvariant())
                .Select(x => new { x.Id, x.UserName })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Bu istifadəçi adı ilə hesab tapılmadı.");

            if (invitedUser.Id == ownerUserId)
                throw new BadRequestException("Siyahı sahibi əməkdaş kimi əlavə edilə bilməz.");

            var hasBlock = await _context.UserBlocks
                .AsNoTracking()
                .AnyAsync(x =>
                    (x.BlockerId == ownerUserId && x.BlockedUserId == invitedUser.Id) ||
                    (x.BlockerId == invitedUser.Id && x.BlockedUserId == ownerUserId),
                    cancellationToken);

            if (hasBlock)
                throw new ForbiddenException("Bu istifadəçini siyahıya əməkdaş kimi əlavə etmək mümkün deyil.");

            var existing = await _context.GameListCollaborators
                .FirstOrDefaultAsync(x => x.GameListId == listId && x.UserId == invitedUser.Id, cancellationToken);

            if (existing is not null)
            {
                throw new ConflictException(existing.Status == GameListCollaborationStatus.Accepted
                    ? "Bu istifadəçi artıq siyahının əməkdaşıdır."
                    : "Bu istifadəçiyə artıq əməkdaşlıq dəvəti göndərilib.");
            }

            await _context.GameListCollaborators.AddAsync(new GameListCollaborator
            {
                GameListId = listId,
                UserId = invitedUser.Id,
                InvitedByUserId = ownerUserId,
                Status = GameListCollaborationStatus.Pending,
                InvitedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            await _notificationService.QueueAsync(new NotificationCreateRequest
            {
                RecipientUserId = invitedUser.Id,
                ActorUserId = ownerUserId,
                Type = NotificationType.GameListCollaborationInvited,
                EntityType = NotificationEntityType.GameList,
                EntityId = listId.ToString(),
                Title = "Siyahı əməkdaşlığı dəvəti",
                Message = $"{gameList.Name} siyahısına əməkdaş kimi dəvət edildin.",
                ActionUrl = $"/lists/{listId}",
                BypassPreferences = true
            }, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task AcceptCollaborationAsync(
            int listId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var collaboration = await _context.GameListCollaborators
                .Include(x => x.GameList)
                .FirstOrDefaultAsync(x => x.GameListId == listId && x.UserId == userId, cancellationToken)
                ?? throw new NotFoundException("Əməkdaşlıq dəvəti tapılmadı.");

            if (collaboration.Status == GameListCollaborationStatus.Accepted)
                return;

            collaboration.Status = GameListCollaborationStatus.Accepted;
            collaboration.RespondedAtUtc = DateTime.UtcNow;

            await _notificationService.QueueAsync(new NotificationCreateRequest
            {
                RecipientUserId = collaboration.GameList.UserId,
                ActorUserId = userId,
                Type = NotificationType.GameListCollaborationAccepted,
                EntityType = NotificationEntityType.GameList,
                EntityId = listId.ToString(),
                Title = "Əməkdaşlıq dəvəti qəbul edildi",
                Message = $"{collaboration.GameList.Name} siyahısı üçün əməkdaşlıq dəvəti qəbul edildi.",
                ActionUrl = $"/lists/{listId}",
                BypassPreferences = true
            }, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeclineCollaborationAsync(
            int listId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var collaboration = await _context.GameListCollaborators
                .FirstOrDefaultAsync(x => x.GameListId == listId && x.UserId == userId, cancellationToken)
                ?? throw new NotFoundException("Əməkdaşlıq dəvəti tapılmadı.");

            if (collaboration.Status == GameListCollaborationStatus.Accepted)
                throw new BadRequestException("Qəbul edilmiş əməkdaşlıqdan çıxmaq üçün siyahını tərk et əməliyyatından istifadə edin.");

            _context.GameListCollaborators.Remove(collaboration);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveCollaboratorAsync(
            int listId,
            Guid actingUserId,
            Guid collaboratorUserId,
            CancellationToken cancellationToken = default)
        {
            var gameList = await _context.GameLists
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == listId, cancellationToken)
                ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

            if (actingUserId != gameList.UserId && actingUserId != collaboratorUserId)
                throw new ForbiddenException("Bu əməkdaşı siyahıdan çıxarmaq icazəniz yoxdur.");

            var collaboration = await _context.GameListCollaborators
                .FirstOrDefaultAsync(x => x.GameListId == listId && x.UserId == collaboratorUserId, cancellationToken)
                ?? throw new NotFoundException("Əməkdaş tapılmadı.");

            _context.GameListCollaborators.Remove(collaboration);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private Task<int> CountPlayedGamesAsync(
            int listId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return (
                from item in _context.GameListItems.AsNoTracking()
                join userGame in _context.UserGames.AsNoTracking()
                    on new
                    {
                        Source = item.GameSource,
                        GameId = item.GameSourceId
                    }
                    equals new
                    {
                        Source = userGame.Source,
                        GameId = userGame.GameId
                    }
                where item.GameListId == listId &&
                      userGame.UserId == userId &&
                      userGame.Status == UserGameStatus.Played
                select new
                {
                    item.GameSource,
                    item.GameSourceId
                })
                .Distinct()
                .CountAsync(cancellationToken);
        }

        private async Task<GameList> GetEditableGameListAsync(
            int listId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            listId.EnsureValidId();

            var gameList = await _context.GameLists
                .FirstOrDefaultAsync(x => x.Id == listId, cancellationToken)
                ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

            if (gameList.UserId == userId)
                return gameList;

            var isCollaborator = await _context.GameListCollaborators
                .AsNoTracking()
                .AnyAsync(x =>
                    x.GameListId == listId &&
                    x.UserId == userId &&
                    x.Status == GameListCollaborationStatus.Accepted,
                    cancellationToken);

            if (!isCollaborator)
                throw new ForbiddenException("Bu oyun siyahısının oyunlarını dəyişmək icazəniz yoxdur.");

            return gameList;
        }

        private async Task<GameList> GetUserGameListAsync(
            int id,
            Guid userId,
            bool includeItems,
            CancellationToken cancellationToken)
        {
            id.EnsureValidId();

            IQueryable<GameList> query = _context.GameLists;

            if (includeItems)
                query = query.Include(x => x.GameListItems);

            var gameList = await query
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

            if (gameList.UserId != userId)
                throw new ForbiddenException("Bu oyun siyahısını dəyişmək icazəniz yoxdur.");

            return gameList;
        }

        private async Task CheckListAccessAsync(
            Guid ownerId,
            ListVisibility visibility,
            Guid? currentUserId,
            GameListCollaborationStatus? collaborationStatus,
            CancellationToken cancellationToken)
        {
            await CheckBlockAccessAsync(ownerId, currentUserId, cancellationToken);

            if (ownerId == currentUserId ||
                visibility == ListVisibility.Public ||
                collaborationStatus is GameListCollaborationStatus.Pending or GameListCollaborationStatus.Accepted)
            {
                return;
            }

            if (visibility == ListVisibility.Private)
                throw new ForbiddenException("Bu oyun siyahısı gizlidir.");

            if (!currentUserId.HasValue)
                throw new ForbiddenException("Bu siyahını yalnız izləyicilər görə bilər.");

            var isFollowing = await _context.UserFollows
                .AsNoTracking()
                .AnyAsync(x =>
                    x.FollowerId == currentUserId.Value &&
                    x.FollowingId == ownerId &&
                    x.Status == FollowStatus.Accepted,
                    cancellationToken);

            if (!isFollowing)
                throw new ForbiddenException("Bu siyahını yalnız izləyicilər görə bilər.");
        }

        private async Task CheckBlockAccessAsync(
            Guid ownerId,
            Guid? currentUserId,
            CancellationToken cancellationToken)
        {
            if (!currentUserId.HasValue || currentUserId.Value == ownerId)
                return;

            var hasBlock = await _context.UserBlocks
                .AsNoTracking()
                .AnyAsync(x =>
                    (x.BlockerId == currentUserId.Value && x.BlockedUserId == ownerId) ||
                    (x.BlockerId == ownerId && x.BlockedUserId == currentUserId.Value),
                    cancellationToken);

            if (hasBlock)
                throw new ForbiddenException("Bu istifadəçinin oyun siyahılarına baxmaq mümkün deyil.");
        }

        private async Task EnsureListNameIsAvailableAsync(
            Guid userId,
            string name,
            int? excludedId,
            CancellationToken cancellationToken)
        {
            var exists = await _context.GameLists
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.UserId == userId &&
                    (!excludedId.HasValue || x.Id != excludedId.Value) &&
                    x.Name == name,
                    cancellationToken);

            if (exists)
                throw new ConflictException("Bu adda oyun siyahısı artıq mövcuddur.");
        }

        private async Task SaveListChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException("Bu adda oyun siyahısı artıq mövcuddur.");
            }
        }
    }
}
