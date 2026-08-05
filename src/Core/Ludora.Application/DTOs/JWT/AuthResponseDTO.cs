using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ludora.Application.DTOs;

namespace Ludora.Application.DTOs.JWT
{
    public record AuthResponseDTO
    {
        public string AccessToken { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public CurrentUserDTO User { get; set; } = null!;
    }
}
