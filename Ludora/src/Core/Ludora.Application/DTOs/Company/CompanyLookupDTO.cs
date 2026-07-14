using Ludora.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record CompanyLookupDTO
    {
        public CatalogSource Source { get; init; }
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public GameCompanyRole Role { get; init; }
        public bool IsOverridden { get; init; }
    }
}
