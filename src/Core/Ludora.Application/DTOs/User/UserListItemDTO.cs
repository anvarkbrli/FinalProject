using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record UserListItemDTO
    {
        public Guid Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? ProfileImageUrl { get; set; }

        public bool IsFollowing { get; set; }
    }
}
