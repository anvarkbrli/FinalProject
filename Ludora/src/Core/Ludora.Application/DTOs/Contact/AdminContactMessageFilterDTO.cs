using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Contact;

public sealed record AdminContactMessageFilterDTO
{
    public string? Search { get; init; }
    public string? Subject { get; init; }
    public ContactMessageStatus? Status { get; init; }
    public string? SortBy { get; init; } = "newest";
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
