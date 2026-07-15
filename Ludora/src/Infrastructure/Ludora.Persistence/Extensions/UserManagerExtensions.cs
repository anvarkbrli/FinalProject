namespace Ludora.Persistence.Extensions;

using Ludora.Application.Exceptions;
using Ludora.Domain.Entities.User;
using Microsoft.AspNetCore.Identity;

public static class UserManagerExtensions
{
    public static async Task<AppUser> FindByIdOrThrowAsync(this UserManager<AppUser> userManager, Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        return user ?? throw new NotFoundException("İstifadəçi tapılmadı.");
    }

    public static async Task<AppUser> FindByNameOrThrowAsync(this UserManager<AppUser> userManager, string userName)
    {
        var user = await userManager.FindByNameAsync(userName.Trim());

        return user ?? throw new NotFoundException("İstifadəçi tapılmadı.");
    }
}