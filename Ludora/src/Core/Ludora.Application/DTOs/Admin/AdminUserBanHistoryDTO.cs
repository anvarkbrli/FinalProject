
namespace Ludora.Application.DTOs.Admin
{
    public record AdminUserBanHistoryDTO
    {
        public Guid UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public int TotalBanCount { get; init; }
        public int TemporaryBanCount { get; init; }
        public int PermanentBanCount { get; init; }
        public IReadOnlyList<AdminUserBanHistoryItemDTO> Items { get; init; } = [];
    }
}
