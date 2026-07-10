namespace Ludora.Persistence.Extensions;

using Ludora.Application.Exceptions;
using Microsoft.AspNetCore.Identity;

public static class IdentityResultExtensions
{
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (result.Succeeded) return;

        var errors = string.Join(" ",result.Errors.Select(x => x.Description));

        throw new BadRequestException(errors);
    }
}