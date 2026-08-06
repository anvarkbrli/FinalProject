using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ludora.Application.DTOs.JWT;
using Ludora.Domain.Entities.User;

namespace Ludora.Application.Interfaces
{
    public interface IJwtTokenService
    {
        JwtTokenDTO CreateToken(AppUser user, IEnumerable<string> roles, Guid sessionId);
    }
}
