using System;
using System.Collections.Generic;
using System.Linq;

using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public  record CompanyDetailDTO
    {

        public CatalogSource Source { get; init; }

        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool IsOverridden { get; init; }

        public DateTime? CreatedAt { get; init; }

        public DateTime? UpdatedAt { get; init; }
    }
}
