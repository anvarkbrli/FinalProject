using Ludora.Application.Exceptions;
using Ludora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Persistence.Extensions
{
    public static class CompanyQueryExtensions
    {
        public static async Task EnsureNameIsAvailableAsync<TEntity>(
        this IQueryable<TEntity> query,
        string name,
        string entityName,
        int? excludedId = null)
        where TEntity : class
        {
            var isDeleted = await query
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(entity =>
                    (!excludedId.HasValue ||
                     EF.Property<int>(entity, "Id") != excludedId.Value) &&
                    EF.Property<string>(entity, "Name") == name)
                .OrderBy(entity =>
                    EF.Property<bool>(entity, "IsDeleted"))
                .Select(entity =>
                    (bool?)EF.Property<bool>(entity, "IsDeleted"))
                .FirstOrDefaultAsync();

            if (isDeleted is null)
                return;

            if (isDeleted.Value)
            {
                throw new ConflictException(
                    $"Bu adda silinmiş {entityName} mövcuddur. " +
                    "Yeni qeyd yaratmaq əvəzinə onu bərpa edin.");
            }

            throw new ConflictException(
                $"Bu adda {entityName} artıq mövcuddur.");
        }
    }
}
