using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Mappings;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly AppDbContext _context;

        public UserService(UserManager<AppUser> userManager, IFileStorageService fileStorageService, AppDbContext context)
        {
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _context = context;
        }

        public async Task<CurrentUserDTO> GetCurrentUserAsync(Guid userId)
        {
            var user = await _userManager.FindByIdOrThrowAsync(userId);

            var followerCount = await _context.UserFollows.CountAsync(uf => uf.FollowingId == userId && uf.Status == FollowStatus.Accepted);

            var followingCount = await _context.UserFollows.CountAsync(uf => uf.FollowerId == userId && uf.Status == FollowStatus.Accepted);

            return new CurrentUserDTO
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Bio = user.Bio,
                ProfileImageUrl = user.ProfileImageUrl,
                IsPrivate = user.IsPrivate,
                CreatedAt = user.CreatedAt,
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                PostCount = 0
            };
        }

        public async Task<UserProfileDTO> GetByUserNameAsync(string userName, Guid? currentUserId)
        {
            if (string.IsNullOrWhiteSpace(userName)) throw new BadRequestException("İstifadəçi adı boş ola bilməz.");

            var user = await _userManager.Users
                .AsNoTracking()
                .Include(u => u.Followers)
                .Include(u => u.Following)
                .FirstOrDefaultAsync(u => u.NormalizedUserName == userName.Trim().ToUpper());

            if (user is null)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            if (currentUserId.HasValue && currentUserId.Value != user.Id)
            {
                var hasBlock = await _context.UserBlocks.AnyAsync(ub =>
                    ub.BlockerId == currentUserId.Value && ub.BlockedUserId == user.Id ||
                    ub.BlockerId == user.Id && ub.BlockedUserId == currentUserId.Value);

                if (hasBlock) throw new ForbiddenException("Bu profilə baxmaq mümkün deyil.");
            }

            return new UserProfileDTO
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Bio = user.Bio,
                ProfileImageUrl = user.ProfileImageUrl,
                IsPrivate = user.IsPrivate,
                CreatedAt = user.CreatedAt,
                FollowerCount = await _context.UserFollows.CountAsync(uf => uf.FollowerId == user.Id && uf.Status == FollowStatus.Accepted),
                FollowingCount = await _context.UserFollows.CountAsync(uf => uf.FollowingId == user.Id && uf.Status == FollowStatus.Accepted),
                PostCount = 0
            };
        }

        public async Task<CurrentUserDTO> UpdateProfileAsync(Guid userId, UpdateProfileDTO dto)
        {
            var user = await _userManager.FindByIdOrThrowAsync(userId);
            var userName = dto.UserName.Trim();

            var existingUser = await _userManager.FindByNameAsync(userName);

            if (existingUser is not null && existingUser.Id != userId)
                throw new ConflictException("Bu istifadəçi adı artıq istifadə olunur.");

            user.UserName = userName;
            user.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();

            var result = await _userManager.UpdateAsync(user);

            result.ThrowIfFailed();

            return user.ToCurrentUserDTO();
        }

        public async Task<CurrentUserDTO> UpdateProfileImageAsync(Guid userId, ProfileImageUploadDTO dto)
        {
            var user = await _userManager.FindByIdOrThrowAsync(userId);
            var oldImagePath = user.ProfileImageUrl;
            var newImagePath = await _fileStorageService.SaveProfileImageAsync(dto);

            user.ProfileImageUrl = newImagePath;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                await _fileStorageService.DeleteAsync(newImagePath);
                result.ThrowIfFailed();
            }

            await _fileStorageService.DeleteAsync(oldImagePath);

            return user.ToCurrentUserDTO();
        }

        public async Task<UserSettingsDTO> GetSettingsAsync(Guid userId)
        {
            var user = await _userManager.FindByIdOrThrowAsync(userId);

            return new UserSettingsDTO
            {
                IsPrivate = user.IsPrivate
            };
        }

        public async Task<UserSettingsDTO> UpdateSettingsAsync(Guid userId, UpdateUserSettingsDTO dto)
        {
            var user = await _userManager.FindByIdOrThrowAsync(userId);

            if (dto.IsPrivate.HasValue)
                user.IsPrivate = dto.IsPrivate.Value;

            var result = await _userManager.UpdateAsync(user);

            result.ThrowIfFailed();

            return new UserSettingsDTO
            {
                IsPrivate = user.IsPrivate
            };
        }
    }
}