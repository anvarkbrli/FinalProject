namespace Ludora.Application.Models;

public sealed record IgdbCompanyModel
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
