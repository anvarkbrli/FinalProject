using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Mappings;
using Ludora.Domain.Entities;
using Ludora.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IFileStorageService _fileStorageService;

        public UserService(UserManager<AppUser> userManager, IFileStorageService fileStorageService)
        {
            _userManager = userManager;
            _fileStorageService = fileStorageService;
        }

        public async Task<CurrentUserDTO> GetCurrentUserAsync(Guid userId)
        {
            var user = await _userManager.FindByIdOrThrowAsync(userId);

            return user.ToCurrentUserDTO();
        }

        public async Task<UserProfileDTO> GetByUserNameAsync(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new BadRequestException("İstifadəçi adı boş ola bilməz.");

            var user = await _userManager.Users
                .AsNoTracking()
                .Include(u => u.Followers)
                .Include(u => u.Following)
                .FirstOrDefaultAsync(u => u.NormalizedUserName == userName.Trim().ToUpper());

            if (user is null)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            return new UserProfileDTO
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Bio = user.Bio,
                ProfileImageUrl = user.ProfileImageUrl,
                IsPrivate = user.IsPrivate,
                CreatedAt = user.CreatedAt,
                FollowerCount = user.Followers.Count,
                FollowingCount = user.Following.Count,
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
    }
}