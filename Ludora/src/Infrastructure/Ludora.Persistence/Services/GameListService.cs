using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public class GameListService : IGameListService
    {
        private readonly AppDbContext _context;

        public GameListService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<GameListSummaryDTO>> GetUserListsAsync(Guid userId, Guid? currentUserId)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);

            if (!userExists) throw new NotFoundException("İstifadəçi tapılmadı.");

            var query = _context.GameLists
                .AsNoTracking()
                .Where(gl => gl.UserId == userId);

            if (currentUserId != userId)
            {
                var isFollowing = currentUserId.HasValue && await _context.UserFollows.AnyAsync(f =>
                    f.FollowerId == currentUserId.Value && f.FollowingId == userId);

                query = isFollowing
                    ? query.Where(gl => gl.Visibility != ListVisibility.Private)
                    : query.Where(gl => gl.Visibility == ListVisibility.Public);
            }

            return await query
                .OrderByDescending(gl => gl.CreatedAt)
                .Select(gl => new GameListSummaryDTO
                {
                    Id = gl.Id,
                    Name = gl.Name,
                    Description = gl.Description,
                    Visibility = gl.Visibility,
                    OwnerId = gl.UserId,
                    OwnerUserName = gl.User.UserName ?? string.Empty,
                    GameCount = gl.GameListItems.Count,
                    CreatedAt = gl.CreatedAt,
                    UpdatedAt = gl.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<GameListDetailDTO> GetByIdAsync(int id, Guid? currentUserId)
        {
            var gameList = await _context.GameLists
                .AsNoTracking()
                .Include(gl => gl.User)
                .Include(gl => gl.GameListItems)
                .ThenInclude(gli => gli.Game)
                .FirstOrDefaultAsync(gl => gl.Id == id);

            if (gameList is null) throw new NotFoundException("Oyun siyahısı tapılmadı.");

            await CheckListAccessAsync(gameList, currentUserId);

            return new GameListDetailDTO
            {
                Id = gameList.Id,
                Name = gameList.Name,
                Description = gameList.Description,
                Visibility = gameList.Visibility,
                OwnerId = gameList.UserId,
                OwnerUserName = gameList.User.UserName ?? string.Empty,
                IsOwner = currentUserId == gameList.UserId,
                GameCount = gameList.GameListItems.Count,
                CreatedAt = gameList.CreatedAt,
                UpdatedAt = gameList.UpdatedAt,
                Games = gameList.GameListItems
                    .OrderByDescending(gli => gli.AddedAt)
                    .Select(gli => new GameListEntryDTO
                    {
                        Id = gli.Game.Id,
                        Title = gli.Game.Name,
                        CoverImageUrl = gli.Game.CoverImageUrl,
                        ReleaseDate = gli.Game.ReleaseDate,
                        AverageRating = gli.Game.AverageRating,
                        AddedAt = gli.AddedAt
                    })
                    .ToList()
            };
        }

        public async Task<int> CreateAsync(Guid userId, GameListCreateDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new BadRequestException("Siyahının adı boş ola bilməz.");

            var name = dto.Name.Trim();

            var listExists = await _context.GameLists.AnyAsync(gl =>
                gl.UserId == userId && gl.Name.ToLower() == name.ToLower());

            if (listExists)
                throw new ConflictException("Bu adda oyun siyahısı artıq mövcuddur.");

            var gameList = new GameList
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                Visibility = dto.Visibility,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _context.GameLists.AddAsync(gameList);
            await _context.SaveChangesAsync();

            return gameList.Id;
        }

        public async Task UpdateAsync(int id, Guid userId, GameListUpdateDTO dto)
        {
            var gameList = await GetUserGameListAsync(id, userId);

            if (dto.Name is not null)
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    throw new BadRequestException("Siyahının adı boş ola bilməz.");

                var name = dto.Name.Trim();

                var listExists = await _context.GameLists.AnyAsync(gl =>
                    gl.UserId == userId && gl.Id != id && gl.Name.ToLower() == name.ToLower());

                if (listExists)
                    throw new ConflictException("Bu adda oyun siyahısı artıq mövcuddur.");

                gameList.Name = name;
            }

            if (dto.Description is not null)
                gameList.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

            if (dto.Visibility.HasValue)
                gameList.Visibility = dto.Visibility.Value;

            gameList.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, Guid userId)
        {
            var gameList = await GetUserGameListAsync(id, userId);

            gameList.IsDeleted = true;
            gameList.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task AddGameAsync(int listId, int gameId, Guid userId)
        {
            await GetUserGameListAsync(listId, userId);

            var gameExists = await _context.Games.AnyAsync(g => g.Id == gameId);

            if (!gameExists)
                throw new NotFoundException("Oyun tapılmadı.");

            var itemExists = await _context.GameListItems.AnyAsync(gli =>
                gli.GameListId == listId && gli.GameId == gameId);

            if (itemExists)
                throw new ConflictException("Bu oyun siyahıda artıq mövcuddur.");

            var gameListItem = new GameListItem
            {
                GameListId = listId,
                GameId = gameId,
                AddedAt = DateTime.UtcNow
            };

            await _context.GameListItems.AddAsync(gameListItem);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveGameAsync(int listId, int gameId, Guid userId)
        {
            await GetUserGameListAsync(listId, userId);

            var gameListItem = await _context.GameListItems.FirstOrDefaultAsync(gli =>
                gli.GameListId == listId && gli.GameId == gameId);

            if (gameListItem is null)
                throw new NotFoundException("Oyun bu siyahıda tapılmadı.");

            _context.GameListItems.Remove(gameListItem);
            await _context.SaveChangesAsync();
        }

        private async Task<GameList> GetUserGameListAsync(int id, Guid userId)
        {
            var gameList = await _context.GameLists.FirstOrDefaultAsync(gl => gl.Id == id);

            if (gameList is null)
                throw new NotFoundException("Oyun siyahısı tapılmadı.");

            if (gameList.UserId != userId)
                throw new ForbiddenException("Bu oyun siyahısını dəyişmək icazəniz yoxdur.");

            return gameList;
        }

        private async Task CheckListAccessAsync(GameList gameList, Guid? currentUserId)
        {
            if (gameList.UserId == currentUserId || gameList.Visibility == ListVisibility.Public)
                return;

            if (gameList.Visibility == ListVisibility.Private)
                throw new ForbiddenException("Bu oyun siyahısı gizlidir.");

            if (!currentUserId.HasValue)
                throw new ForbiddenException("Bu siyahını yalnız izləyicilər görə bilər.");

            var isFollowing = await _context.UserFollows.AnyAsync(f =>
                f.FollowerId == currentUserId.Value && f.FollowingId == gameList.UserId);

            if (!isFollowing)
                throw new ForbiddenException("Bu siyahını yalnız izləyicilər görə bilər.");
        }
    }
}