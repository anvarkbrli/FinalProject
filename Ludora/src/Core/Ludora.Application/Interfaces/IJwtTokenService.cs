using Ludora.Application.DTOs.JWT;
using Ludora.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.Interfaces
{
    public interface IJwtTokenService
    {
        JwtTokenDTO CreateToken(AppUser user,IEnumerable<string> roles);
    }
}
