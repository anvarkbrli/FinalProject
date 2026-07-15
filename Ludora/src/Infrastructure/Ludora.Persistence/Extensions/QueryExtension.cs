namespace Ludora.Persistence.Extensions;

using Ludora.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

public static class QueryExtensions
{
    public static async Task EnsureNameIsAvailableAsync<TEntity>(
        this IQueryable<TEntity> query,
        string name,
        string entityName,
        int? excludedId = null,
        CancellationToken cancellationToken = default)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (isDeleted is null)
            return;

        if (isDeleted.Value)
        {
            throw new ConflictException(
                $"Bu adda silinmiş {entityName.ToLowerInvariant()} mövcuddur. " +
                "Yeni qeyd yaratmaq əvəzinə onu bərpa edin.");
        }

        throw new ConflictException(
            $"Bu adda {entityName.ToLowerInvariant()} artıq mövcuddur.");
    }
}
