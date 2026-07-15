using Ludora.Domain.Enums;

namespace Ludora.Application.Models;

public sealed record IgdbCompanyRelationModel
{
    public IgdbCompanyModel Company { get; init; } = new();
    public GameCompanyRole Role { get; init; }
}
