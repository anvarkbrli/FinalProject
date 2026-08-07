namespace Ludora.Application.Extensions
{
    using Ludora.Application.DTOs;
    using Ludora.Application.Exceptions;
    using Microsoft.EntityFrameworkCore;

    public static class QueryExtensions
    {
        private const int MaxPageSize = 100;

        public static async Task<PagedResultDTO<T>> ToPagedResultAsync<T>(
            this IQueryable<T> query,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pagination);

            if (pagination.PageNumber < 1)
                throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

            if (pagination.PageSize is < 1 or > MaxPageSize)
            {
                throw new BadRequestException(
                    $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
            }

            var skip = checked((long)(pagination.PageNumber - 1) * pagination.PageSize);

            if (skip > int.MaxValue)
                throw new BadRequestException("Sorğu edilən səhifə aralığı çox böyükdür.");

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((int)skip)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResultDTO<T>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0
                    ? 0
                    : (int)Math.Ceiling(totalCount / (double)pagination.PageSize)
            };
        }

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
}
