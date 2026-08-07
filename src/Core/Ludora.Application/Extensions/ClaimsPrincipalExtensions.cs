using System.Security.Claims;

namespace Ludora.Application.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetRequiredUserId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(value, out var userId))
                throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");

            return userId;
        }
    }
}
