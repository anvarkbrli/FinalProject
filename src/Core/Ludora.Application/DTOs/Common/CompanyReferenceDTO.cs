using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record CompanyReferenceDTO : CatalogReferenceDTO
    {
        public GameCompanyRole Role { get; init; }
    }
}
