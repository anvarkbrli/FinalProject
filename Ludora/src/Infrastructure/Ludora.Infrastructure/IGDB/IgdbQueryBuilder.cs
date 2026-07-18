using System.Globalization;
using System.Text;
using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Infrastructure.IGDB;

internal static class IgdbQueryBuilder
{
    private const string GameFields =
        "fields id,name,summary,first_release_date,total_rating," +
        "game_type,parent_game,dlcs,expansions,standalone_expansions," +
        "bundles,remakes,remasters," +
        "cover.image_id,genres.id,genres.name,platforms.id,platforms.name," +
        "involved_companies.company.id,involved_companies.company.name," +
        "involved_companies.developer,involved_companies.publisher;";

    public static string BuildGames(GameFilterDTO filter)
    {
        var builder = new StringBuilder(GameFields);
        var where = new List<string>
        {
            "version_parent = null"
        };

        if (!filter.IncludeRelatedContent)
        {
            where.Add(
                "game_type = (0,4,8,9,10,11)"
            );
        }



        if (!string.IsNullOrWhiteSpace(filter.SearchName))
            builder.Append(" search \"").Append(Escape(filter.SearchName.Trim())).Append("\";");

        if (filter.GenreId.HasValue &&
            (!filter.GenreSource.HasValue || filter.GenreSource == CatalogSource.Igdb))
        {
            where.Add($"genres = {filter.GenreId.Value}");
        }

        if (filter.PlatformId.HasValue &&
            (!filter.PlatformSource.HasValue || filter.PlatformSource == CatalogSource.Igdb))
        {
            where.Add($"platforms = {filter.PlatformId.Value}");
        }

        if (filter.CompanyId.HasValue &&
            (!filter.CompanySource.HasValue || filter.CompanySource == CatalogSource.Igdb))
        {
            where.Add($"involved_companies.company = {filter.CompanyId.Value}");
        }

        if (filter.ReleaseYear.HasValue)
        {
            var start = new DateTimeOffset(
                filter.ReleaseYear.Value,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero).ToUnixTimeSeconds();

            var end = new DateTimeOffset(
                filter.ReleaseYear.Value + 1,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero).ToUnixTimeSeconds();

            where.Add($"first_release_date >= {start}");
            where.Add($"first_release_date < {end}");
        }

        builder.Append(" where ").Append(string.Join(" & ", where)).Append(';');

        var sort = filter.SortBy?.Trim().ToLowerInvariant() switch
        {
            "name_asc" => "name asc",
            "name_desc" => "name desc",
            "rating_asc" => "total_rating asc",
            "rating_desc" => "total_rating desc",
            "year_asc" => "first_release_date asc",
            "year_desc" => "first_release_date desc",
            _ when string.IsNullOrWhiteSpace(filter.SearchName) => "first_release_date desc",
            _ => null
        };

        if (sort is not null)
            builder.Append(" sort ").Append(sort).Append(';');

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 500);
        var offset = (pageNumber - 1) * pageSize;

        builder.Append(" limit ").Append(pageSize.ToString(CultureInfo.InvariantCulture)).Append(';');
        builder.Append(" offset ").Append(offset.ToString(CultureInfo.InvariantCulture)).Append(';');

        return builder.ToString();
    }

    public static string BuildGamesByIds(IReadOnlyCollection<long> ids)
    {
        return BuildByIds(GameFields, ids);
    }

    public static string BuildNamedList(string? search, int limit = 200)
    {
        var builder = new StringBuilder("fields id,name;");

        if (!string.IsNullOrWhiteSpace(search))
        {
            builder.Append(" where name ~ *\"")
                .Append(Escape(search.Trim()))
                .Append("\"*;");
        }

        builder.Append(" sort name asc;");
        builder.Append(" limit ").Append(Math.Clamp(limit, 1, 500)).Append(';');

        return builder.ToString();
    }

    public static string BuildNamedByIds(IReadOnlyCollection<long> ids)
    {
        return BuildByIds("fields id,name;", ids);
    }

    private static string BuildByIds(string fields, IReadOnlyCollection<long> ids)
    {
        var normalizedIds = ids
            .Where(id => id > 0)
            .Distinct()
            .Take(500)
            .ToArray();

        if (normalizedIds.Length == 0)
            return $"{fields} where id = -1; limit 1;";

        return $"{fields} where id = ({string.Join(',', normalizedIds)}); limit {normalizedIds.Length};";
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
