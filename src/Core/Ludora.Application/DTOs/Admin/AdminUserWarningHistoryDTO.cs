using Ludora.Application.DTOs;

namespace Ludora.Application.DTOs.Admin
{
    public record AdminUserWarningHistoryDTO
    {
        public Guid UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public int TotalWarningCount { get; init; }
        public int ReadWarningCount { get; init; }
        public int UnreadWarningCount { get; init; }
        public IReadOnlyList<UserWarningDTO> Items { get; init; } = [];
    }
}
