using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record FollowStatusDTO
    {
        public int UserId { get; set; }
        public bool IsFollowing { get; set; }
    }
}
