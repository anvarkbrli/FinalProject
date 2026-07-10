using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record CurrentUserDTO
    {
        public Guid Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? Bio { get; set; }

        public string? ProfileImageUrl { get; set; }

        public bool IsPrivate { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
