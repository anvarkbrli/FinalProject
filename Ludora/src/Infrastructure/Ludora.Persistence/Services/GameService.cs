using System.Globalization;
using System.Text;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Game;
using Ludora.Application.DTOs.GameList;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Helpers;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Domain.Entities;
using Ludora.Domain.Entities.GameTracking;
using Ludora.Domain.Entities.IGDB;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class GameService : IGameService
{
    private const int MaxPageSize = 100;
    private const int MaxCombinedResultWindow = 5000;
    private const string SqlLetterStartPattern =
        "[A-Za-zƏĞİÖŞÜÇəğıöşüç]%";

    private static readonly StringComparer GameTitleComparer =
        StringComparer.Create(
            CultureInfo.GetCultureInfo("az-AZ"),
            ignoreCase: true);

    private static readonly GameType[] MainCatalogGameTypes =
    [
        GameType.MainGame,
        GameType.StandaloneExpansion,
        GameType.Remake,
        GameType.Remaster,
        GameType.ExpandedGame,
        GameType.Port
    ];

    private static readonly HashSet<GameType> AllowedRelationTypes =
    [
        GameType.DlcAddon,
        GameType.Expansion,
        GameType.StandaloneExpansion,
        GameType.Bundle,
        GameType.Remake,
        GameType.Remaster
    ];

    private static readonly HashSet<GameType> ParentRelationTypes =
    [
        GameType.DlcAddon,
        GameType.Expansion,
        GameType.StandaloneExpansion,
        GameType.Remake,
        GameType.Remaster
    ];

    private static readonly HashSet<GameType> SeriesGameTypes =
    [
        GameType.MainGame,
        GameType.StandaloneExpansion,
        GameType.Episode,
        GameType.Season
    ];

    private static readonly HashSet<GameType> FranchiseGameTypes =
    [
        GameType.MainGame
    ];

    private sealed record CommunityRankRow
    {
        public CatalogSource Source { get; init; }
        public long GameId { get; init; }
        public double? AverageRating { get; init; }
        public int RatingCount { get; init; }
        public int PlayedCount { get; init; }
    }

    private sealed record CompanyFilterMatchContext
    {
        public required CatalogSource Source { get; init; }
        public required long Id { get; init; }
        public string? FamilyPrefix { get; init; }

        public bool RequiresFamilyMatch =>
            !string.IsNullOrWhiteSpace(FamilyPrefix);
    }

    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;

    public GameService(AppDbContext context, IIgdbClient igdbClient)
    {
        _context = context;
        _igdbClient = igdbClient;
    }

    public async Task<PagedResultDTO<GameListItemDTO>> GetFilteredGamesAsync(
        GameFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidateFilter(filter);

        var pageNumber = filter.PageNumber;
        var pageSize = filter.PageSize;
        var skip = checked((pageNumber - 1) * pageSize);

        var companyMatchContext =
            await ResolveCompanyFilterMatchContextAsync(
                filter,
                cancellationToken);

        if (
            companyMatchContext?.RequiresFamilyMatch == true &&
            HasAdditionalNarrowingFilter(filter))
        {
            return await GetCompanyFamilyFilteredGamesAsync(
                filter,
                companyMatchContext,
                cancellationToken);
        }

        if (
            IsCommunitySort(filter.SortBy) &&
            string.IsNullOrWhiteSpace(filter.SearchName))
        {
            return await GetCommunitySortedGamesAsync(
                filter,
                cancellationToken);
        }

        if (filter.Source == CatalogSource.Local)
        {
            var localQuery = BuildLocalQuery(filter);
            var localTotalCount = await localQuery.CountAsync(cancellationToken);

            var localGames = await ApplyLocalSort(
                    localQuery,
                    filter.SortBy,
                    filter.SearchName)
                .AsSplitQuery()
                .Include(x => x.GameGenres)
                .Include(x => x.GamePlatforms)
                .Include(x => x.GameCompanies)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var items = (await MapLocalGamesAsync(localGames, cancellationToken))
                .Select(ToListItem)
                .ToList();

            return CreatePagedResult(
                items,
                pageNumber,
                pageSize,
                localTotalCount);
        }

        if (filter.Source == CatalogSource.Igdb)
        {
            var hiddenIgdbGameIds =
                await GetHiddenIgdbGameIdsAsync(
                    cancellationToken);

            var igdbTotalCount = await _igdbClient.GetGamesCountAsync(
                filter,
                hiddenIgdbGameIds,
                cancellationToken);

            IReadOnlyList<IgdbGameModel> igdbGames;

            if (IsAscendingNameSort(filter.SortBy) &&
                string.IsNullOrWhiteSpace(filter.SearchName))
            {
                igdbGames = await GetIgdbLetterFirstNameSliceAsync(
                    filter,
                    skip,
                    pageSize,
                    igdbTotalCount,
                    hiddenIgdbGameIds,
                    cancellationToken);
            }
            else
            {
                igdbGames = await _igdbClient.GetGamesAsync(
                    filter,
                    hiddenIgdbGameIds,
                    cancellationToken);
            }

            var mappedIgdbGames = await MapIgdbGamesAsync(
                igdbGames,
                cancellationToken);

            var items = SortGames(
                    mappedIgdbGames,
                    filter.SortBy,
                    filter.SearchName)
                .Select(ToListItem)
                .ToList();

            return CreatePagedResult(
                items,
                pageNumber,
                pageSize,
                igdbTotalCount);
        }

        var requestedEnd = checked((long)pageNumber * pageSize);

        if (requestedEnd > MaxCombinedResultWindow)
        {
            throw new BadRequestException(
                "Sorğu edilən səhifə aralığı çox böyükdür.");
        }

        var fetchCount = (int)requestedEnd;

        var details = new List<GameDetailDTO>(
            Math.Min(fetchCount * 2, MaxCombinedResultWindow * 2));

        var combinedLocalQuery = BuildLocalQuery(filter);
        var localCount = await combinedLocalQuery.CountAsync(cancellationToken);

        var localCombinedGames = await ApplyLocalSort(
                combinedLocalQuery,
                filter.SortBy,
                filter.SearchName)
            .AsSplitQuery()
            .Include(x => x.GameGenres)
            .Include(x => x.GamePlatforms)
            .Include(x => x.GameCompanies)
            .Take(Math.Min(fetchCount, localCount))
            .ToListAsync(cancellationToken);

        details.AddRange(
            await MapLocalGamesAsync(
                localCombinedGames,
                cancellationToken));

        var igdbCount = 0;

        if (_igdbClient.IsEnabled && CanQueryIgdb(filter))
        {
            var igdbFilter = filter with
            {
                Source = CatalogSource.Igdb
            };

            try
            {
                var hiddenIgdbGameIds =
                    await GetHiddenIgdbGameIdsAsync(
                        cancellationToken);

                igdbCount = await _igdbClient.GetGamesCountAsync(
                    igdbFilter,
                    hiddenIgdbGameIds,
                    cancellationToken);

                IReadOnlyList<IgdbGameModel> igdbGames;

                if (IsAscendingNameSort(filter.SortBy) &&
                    string.IsNullOrWhiteSpace(filter.SearchName))
                {
                    igdbGames = await GetIgdbLetterFirstNameSliceAsync(
                        igdbFilter,
                        0,
                        Math.Min(fetchCount, igdbCount),
                        igdbCount,
                        hiddenIgdbGameIds,
                        cancellationToken);
                }
                else
                {
                    igdbGames = await GetIgdbGamesUpToAsync(
                        igdbFilter,
                        fetchCount,
                        hiddenIgdbGameIds,
                        cancellationToken);
                }

                details.AddRange(
                    await MapIgdbGamesAsync(
                        igdbGames,
                        cancellationToken));
            }
            catch (ExternalServiceException)
            {
                // IGDB müvəqqəti əlçatmazdırsa local nəticələr yenə göstərilir.
            }
        }

        var itemsForPage = SortGames(
                details,
                filter.SortBy,
                filter.SearchName)
            .Skip(skip)
            .Take(pageSize)
            .Select(ToListItem)
            .ToList();

        var totalCount = checked(localCount + igdbCount);

        return CreatePagedResult(
            itemsForPage,
            pageNumber,
            pageSize,
            totalCount);
    }

    private static bool HasAdditionalNarrowingFilter(
        GameFilterDTO filter)
    {
        return
            !string.IsNullOrWhiteSpace(filter.SearchName) ||
            filter.GenreId.HasValue ||
            filter.PlatformId.HasValue ||
            filter.ReleaseYear.HasValue ||
            filter.ReleasedOnly;
    }

    private async Task<PagedResultDTO<GameListItemDTO>>
        GetCompanyFamilyFilteredGamesAsync(
            GameFilterDTO filter,
            CompanyFilterMatchContext companyContext,
            CancellationToken cancellationToken)
    {
        var candidateFilter = filter with
        {
            CompanySource = null,
            CompanyId = null,
            PageNumber = 1,
            PageSize = MaxPageSize
        };

        var matchedGames = new List<GameDetailDTO>();
        var seenReferences = new HashSet<(CatalogSource Source, long Id)>();
        var candidatePageNumber = 1;
        var scannedCount = 0;
        var candidateTotalCount = 0;

        do
        {
            var candidatePage = await GetFilteredGamesAsync(
                candidateFilter with
                {
                    PageNumber = candidatePageNumber
                },
                cancellationToken);

            candidateTotalCount = candidatePage.TotalCount;

            if (candidatePage.Items.Count == 0)
            {
                break;
            }

            var references = candidatePage.Items
                .Select(item => new CatalogReferenceDTO
                {
                    Source = item.Source,
                    Id = item.Id
                })
                .ToList();

            var gameMap = await GetByReferencesAsync(
                references,
                cancellationToken);

            foreach (var item in candidatePage.Items)
            {
                var key = (item.Source, item.Id);

                if (
                    !seenReferences.Add(key) ||
                    !gameMap.TryGetValue(key, out var game) ||
                    !MatchesCompanyFilter(game, companyContext))
                {
                    continue;
                }

                matchedGames.Add(game);
            }

            scannedCount += candidatePage.Items.Count;
            candidatePageNumber++;
        }
        while (
            scannedCount < candidateTotalCount &&
            scannedCount < MaxCombinedResultWindow);

        var skip = checked(
            (filter.PageNumber - 1) * filter.PageSize);

        var items = matchedGames
            .Skip(skip)
            .Take(filter.PageSize)
            .Select(ToListItem)
            .ToList();

        return CreatePagedResult(
            items,
            filter.PageNumber,
            filter.PageSize,
            matchedGames.Count);
    }

    private async Task<CompanyFilterMatchContext?>
        ResolveCompanyFilterMatchContextAsync(
            GameFilterDTO filter,
            CancellationToken cancellationToken)
    {
        if (
            !filter.CompanySource.HasValue ||
            !filter.CompanyId.HasValue)
        {
            return null;
        }

        var reference = new CatalogReferenceDTO
        {
            Source = filter.CompanySource.Value,
            Id = filter.CompanyId.Value
        };

        var companyMap = await ResolveCompanyLookupsAsync(
            [reference],
            cancellationToken,
            allowIgdbFallback: true);

        if (!companyMap.TryGetValue(
                (reference.Source, reference.Id),
                out var company))
        {
            return null;
        }

        var normalizedName = NormalizeSearchText(company.Name);

        return new CompanyFilterMatchContext
        {
            Source = reference.Source,
            Id = reference.Id,
            FamilyPrefix = normalizedName switch
            {
                "rockstargames" => "rockstar",
                _ => null
            }
        };
    }

    private static bool MatchesCompanyFilter(
        GameDetailDTO game,
        CompanyFilterMatchContext context)
    {
        if (game.Companies.Any(company =>
                company.Source == context.Source &&
                company.Id == context.Id))
        {
            return true;
        }

        if (!context.RequiresFamilyMatch)
        {
            return false;
        }

        return game.Companies.Any(company =>
            NormalizeSearchText(company.Name).StartsWith(
                context.FamilyPrefix!,
                StringComparison.Ordinal));
    }

    private static bool IsCommunitySort(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() is
            "rating_asc" or
            "rating_desc" or
            "play_asc" or
            "play_desc";
    }

    private static bool IsAscendingNameSort(string? sortBy)
    {
        return string.Equals(
            sortBy?.Trim(),
            "name_asc",
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PagedResultDTO<GameListItemDTO>> GetCommunitySortedGamesAsync(
        GameFilterDTO filter,
        CancellationToken cancellationToken)
    {
        var pageNumber = filter.PageNumber;
        var pageSize = filter.PageSize;
        var skip = checked((pageNumber - 1) * pageSize);
        var normalizedSort = filter.SortBy!
            .Trim()
            .ToLowerInvariant();

        var userGamesQuery = _context.UserGames.AsNoTracking();

        if (filter.Source.HasValue)
        {
            userGamesQuery = userGamesQuery.Where(
                userGame => userGame.Source == filter.Source.Value);
        }

        userGamesQuery = normalizedSort.StartsWith(
            "rating_",
            StringComparison.Ordinal)
            ? userGamesQuery.Where(userGame => userGame.Rating.HasValue)
            : userGamesQuery.Where(
                userGame => userGame.Status == UserGameStatus.Played);

        if (filter.Source != CatalogSource.Igdb)
        {
            var filteredLocalIds = BuildLocalQuery(filter)
                .Select(game => (long)game.Id);

            userGamesQuery = userGamesQuery.Where(userGame =>
                userGame.Source != CatalogSource.Local ||
                filteredLocalIds.Contains(userGame.GameId));
        }

        var groupedQuery = userGamesQuery
            .GroupBy(userGame => new
            {
                userGame.Source,
                userGame.GameId
            })
            .Select(group => new CommunityRankRow
            {
                Source = group.Key.Source,
                GameId = group.Key.GameId,
                AverageRating = group.Average(
                    userGame => userGame.Rating),
                RatingCount = group.Count(
                    userGame => userGame.Rating.HasValue),
                PlayedCount = group.Count(
                    userGame => userGame.Status == UserGameStatus.Played)
            });

        IQueryable<CommunityRankRow> orderedQuery = normalizedSort switch
        {
            "rating_asc" => groupedQuery
                .OrderBy(row => row.AverageRating ?? double.MaxValue)
                .ThenByDescending(row => row.RatingCount)
                .ThenBy(row => row.Source)
                .ThenBy(row => row.GameId),
            "rating_desc" => groupedQuery
                .OrderByDescending(row => row.AverageRating ?? double.MinValue)
                .ThenByDescending(row => row.RatingCount)
                .ThenBy(row => row.Source)
                .ThenBy(row => row.GameId),
            "play_asc" => groupedQuery
                .OrderBy(row => row.PlayedCount)
                .ThenByDescending(row => row.AverageRating ?? double.MinValue)
                .ThenBy(row => row.Source)
                .ThenBy(row => row.GameId),
            _ => groupedQuery
                .OrderByDescending(row => row.PlayedCount)
                .ThenByDescending(row => row.AverageRating ?? double.MinValue)
                .ThenBy(row => row.Source)
                .ThenBy(row => row.GameId)
        };

        var requiresProviderSidePostFilter =
            filter.Source != CatalogSource.Local &&
            (filter.CompanyId.HasValue ||
             filter.GenreId.HasValue ||
             filter.PlatformId.HasValue ||
             filter.ReleaseYear.HasValue ||
             filter.ReleasedOnly ||
             !string.IsNullOrWhiteSpace(filter.SearchName) ||
             !filter.IncludeRelatedContent);

        if (!requiresProviderSidePostFilter)
        {
            var totalCount = await groupedQuery.CountAsync(cancellationToken);

            var rows = await orderedQuery
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var references = rows
                .Select(row => new CatalogReferenceDTO
                {
                    Source = row.Source,
                    Id = row.GameId
                })
                .ToList();

            var gameMap = await GetByReferencesAsync(
                references,
                cancellationToken);

            var items = rows
                .Select(row => gameMap.GetValueOrDefault(
                    (row.Source, row.GameId)))
                .Where(game => game is not null)
                .Select(game => ToListItem(game!))
                .ToList();

            return CreatePagedResult(
                items,
                pageNumber,
                pageSize,
                totalCount);
        }

        // IGDB metadata-based filters cannot be applied to UserGames in SQL.
        // Scan the ranked references in stable batches, resolve their catalog
        // metadata, and calculate both the page and total count from the same
        // filtered sequence. This prevents cases where the UI reports matches
        // but renders an empty result list.
        const int metadataBatchSize = 200;
        var filteredTotalCount = 0;
        var matchedItems = new List<GameListItemDTO>(pageSize);
        var offset = 0;

        while (true)
        {
            var rows = await orderedQuery
                .Skip(offset)
                .Take(metadataBatchSize)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                break;
            }

            var references = rows
                .Select(row => new CatalogReferenceDTO
                {
                    Source = row.Source,
                    Id = row.GameId
                })
                .ToList();

            var gameMap = await GetByReferencesAsync(
                references,
                cancellationToken);

            foreach (var row in rows)
            {
                if (!gameMap.TryGetValue(
                        (row.Source, row.GameId),
                        out var game) ||
                    !MatchesFilter(game, filter))
                {
                    continue;
                }

                if (filteredTotalCount >= skip &&
                    matchedItems.Count < pageSize)
                {
                    matchedItems.Add(ToListItem(game));
                }

                filteredTotalCount++;
            }

            offset += rows.Count;
        }

        return CreatePagedResult(
            matchedItems,
            pageNumber,
            pageSize,
            filteredTotalCount);
    }

    private static bool MatchesFilter(
        GameDetailDTO game,
        GameFilterDTO filter)
    {
        if (filter.Source.HasValue && game.Source != filter.Source.Value)
        {
            return false;
        }

        if (
            !filter.IncludeRelatedContent &&
            !MainCatalogGameTypes.Contains(game.GameType))
        {
            return false;
        }

        if (
            !string.IsNullOrWhiteSpace(filter.SearchName) &&
            !MatchesSearchText(game.Title, filter.SearchName))
        {
            return false;
        }

        if (
            filter.CompanyId.HasValue &&
            !game.Companies.Any(company =>
                company.Source == filter.CompanySource &&
                company.Id == filter.CompanyId.Value))
        {
            return false;
        }

        if (
            filter.GenreId.HasValue &&
            !game.Genres.Any(genre =>
                genre.Source == filter.GenreSource &&
                genre.Id == filter.GenreId.Value))
        {
            return false;
        }

        if (
            filter.PlatformId.HasValue &&
            !game.Platforms.Any(platform =>
                platform.Source == filter.PlatformSource &&
                platform.Id == filter.PlatformId.Value))
        {
            return false;
        }

        if (
            filter.ReleaseYear.HasValue &&
            game.ReleaseDate?.Year != filter.ReleaseYear.Value)
        {
            return false;
        }

        if (
            filter.ReleasedOnly &&
            (!game.ReleaseDate.HasValue ||
             game.ReleaseDate.Value >
             DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesSearchText(
        string title,
        string searchText)
    {
        var normalizedTitle = NormalizeSearchText(title);
        var normalizedSearch = NormalizeSearchText(searchText);

        if (normalizedSearch.Length == 0)
        {
            return true;
        }

        if (normalizedTitle.Contains(
            normalizedSearch,
            StringComparison.Ordinal))
        {
            return true;
        }

        var acronym = string.Concat(
            title.Split(
                    [' ', '-', '_', ':', '.', ',', '/', '\\', '(', ')', '[', ']'],
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Any(char.IsLetterOrDigit))
                .Select(word => char.ToLowerInvariant(
                    word.First(char.IsLetterOrDigit))));

        return acronym.StartsWith(
            normalizedSearch,
            StringComparison.Ordinal);
    }

    private static string NormalizeSearchText(string value)
    {
        return new string(
            value
                .Normalize(NormalizationForm.FormD)
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    private async Task<IReadOnlyList<IgdbGameModel>>
        GetIgdbLetterFirstNameSliceAsync(
            GameFilterDTO filter,
            int virtualOffset,
            int limit,
            int totalCount,
            IReadOnlyCollection<long> excludedGameIds,
            CancellationToken cancellationToken)
    {
        if (limit <= 0 || virtualOffset >= totalCount)
        {
            return Array.Empty<IgdbGameModel>();
        }

        var alphabeticalFilter = filter with
        {
            Source = CatalogSource.Igdb,
            SortBy = "name_asc",
            PageNumber = 1,
            PageSize = Math.Clamp(limit, 1, 500)
        };

        var nonLetterPrefixCount =
            await GetIgdbLeadingNonLetterCountAsync(
                alphabeticalFilter,
                totalCount,
                excludedGameIds,
                cancellationToken);

        var letterCount = Math.Max(
            0,
            totalCount - nonLetterPrefixCount);

        var remaining = Math.Min(
            limit,
            totalCount - virtualOffset);

        var result = new List<IgdbGameModel>(remaining);
        var currentVirtualOffset = virtualOffset;

        if (currentVirtualOffset < letterCount && remaining > 0)
        {
            var letterTake = Math.Min(
                remaining,
                letterCount - currentVirtualOffset);

            var letterGames = await _igdbClient.GetGamesSliceAsync(
                alphabeticalFilter,
                nonLetterPrefixCount + currentVirtualOffset,
                letterTake,
                excludedGameIds,
                cancellationToken);

            result.AddRange(letterGames);
            remaining -= letterGames.Count;
            currentVirtualOffset += letterGames.Count;
        }

        if (remaining > 0)
        {
            var nonLetterOffset = Math.Max(
                0,
                currentVirtualOffset - letterCount);

            var nonLetterTake = Math.Min(
                remaining,
                nonLetterPrefixCount - nonLetterOffset);

            if (nonLetterTake > 0)
            {
                var nonLetterGames =
                    await _igdbClient.GetGamesSliceAsync(
                        alphabeticalFilter,
                        nonLetterOffset,
                        nonLetterTake,
                        excludedGameIds,
                        cancellationToken);

                result.AddRange(nonLetterGames);
            }
        }

        return result;
    }

    private async Task<int> GetIgdbLeadingNonLetterCountAsync(
        GameFilterDTO filter,
        int totalCount,
        IReadOnlyCollection<long> excludedGameIds,
        CancellationToken cancellationToken)
    {
        const int batchSize = 500;
        var offset = 0;

        while (offset < totalCount)
        {
            var limit = Math.Min(
                batchSize,
                totalCount - offset);

            var batch = await _igdbClient.GetGamesSliceAsync(
                filter,
                offset,
                limit,
                excludedGameIds,
                cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            for (var index = 0; index < batch.Count; index++)
            {
                if (StartsWithLetter(batch[index].Name))
                {
                    return offset + index;
                }
            }

            offset += batch.Count;

            if (batch.Count < limit)
            {
                break;
            }
        }

        return Math.Min(offset, totalCount);
    }

    private async Task<IReadOnlyList<IgdbGameModel>> GetIgdbGamesUpToAsync(
        GameFilterDTO filter,
        int requestedCount,
        IReadOnlyCollection<long> excludedGameIds,
        CancellationToken cancellationToken)
    {
        if (requestedCount <= 0)
            return Array.Empty<IgdbGameModel>();

        const int maximumBatchSize = 500;
        var games = new List<IgdbGameModel>(requestedCount);
        var offset = 0;

        while (offset < requestedCount)
        {
            var limit = Math.Min(
                maximumBatchSize,
                requestedCount - offset);

            var batch = await _igdbClient.GetGamesSliceAsync(
                filter,
                offset,
                limit,
                excludedGameIds,
                cancellationToken);

            games.AddRange(batch);

            if (batch.Count < limit)
                break;

            offset += limit;
        }

        return games;
    }

    private async Task<IReadOnlyList<long>> GetHiddenIgdbGameIdsAsync(
        CancellationToken cancellationToken)
    {
        return await _context.IgdbGameOverrides
            .AsNoTracking()
            .Where(x => x.IsHidden)
            .Select(x => x.IgdbGameId)
            .ToListAsync(cancellationToken);
    }

    private static PagedResultDTO<GameListItemDTO> CreatePagedResult(
        IReadOnlyList<GameListItemDTO> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        return new PagedResultDTO<GameListItemDTO>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<GameDetailDTO> GetByIdAsync(CatalogSource source, long id, CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Oyun");

        if (source == CatalogSource.Local)
        {
            if (id > int.MaxValue)
            {
                throw new NotFoundException("Oyun tapılmadı.");
            }

            var game = await _context.Games
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.GameGenres)
                .Include(x => x.GamePlatforms)
                .Include(x => x.GameCompanies)
                .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                ?? throw new NotFoundException("Oyun tapılmadı.");

            var localDetail = (await MapLocalGamesAsync([game], cancellationToken)).Single();
            var relatedGames = await GetManualRelatedGamesAsync(
                CatalogSource.Local,
                id,
                cancellationToken);

            return localDetail with
            {
                RelatedGames = relatedGames
            };
        }

        var igdbGame = await _igdbClient.GetGameByIdAsync(
            id,
            cancellationToken)
            ?? throw new NotFoundException("IGDB oyunu tapılmadı.");

        var mapped = await MapIgdbGamesAsync(
            [igdbGame],
            cancellationToken);

        var igdbDetail = mapped.FirstOrDefault()
            ?? throw new NotFoundException("Oyun gizlədilib.");

        var igdbRelatedGames = await GetIgdbRelatedGamesAsync(
            igdbGame,
            cancellationToken);

        var manualRelatedGames = await GetManualRelatedGamesAsync(
            CatalogSource.Igdb,
            id,
            cancellationToken);

        return igdbDetail with
        {
            RelatedGames = MergeRelatedGames(
                igdbRelatedGames,
                manualRelatedGames)
        };
    }

    public async Task<int> AddRelationAsync(
        CatalogSource baseSource,
        long baseId,
        GameRelationCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        baseId.EnsureValidCatalogReference(baseSource, "Əsas oyun");
        dto.RelatedId.EnsureValidCatalogReference(dto.RelatedSource, "Əlaqəli oyun");
        ValidateRelationType(dto.RelationType);

        if (baseSource == dto.RelatedSource && baseId == dto.RelatedId)
            throw new BadRequestException("Oyun özü ilə əlaqələndirilə bilməz.");

        var references = new List<CatalogReferenceDTO>
        {
            new() { Source = baseSource, Id = baseId },
            new() { Source = dto.RelatedSource, Id = dto.RelatedId }
        };

        var resolvedGames = await GetByReferencesAsync(references, cancellationToken);

        if (!resolvedGames.ContainsKey((baseSource, baseId)))
            throw new NotFoundException("Əsas oyun tapılmadı.");

        if (!resolvedGames.ContainsKey((dto.RelatedSource, dto.RelatedId)))
            throw new NotFoundException("Əlaqəli oyun tapılmadı.");

        var alreadyExists = await _context.GameRelations.AnyAsync(x =>
            x.BaseGameSource == baseSource &&
            x.BaseGameId == baseId &&
            x.RelatedGameSource == dto.RelatedSource &&
            x.RelatedGameId == dto.RelatedId &&
            x.RelationType == dto.RelationType,
            cancellationToken);

        if (alreadyExists)
            throw new ConflictException("Bu oyun əlaqəsi artıq mövcuddur.");

        var relation = new GameRelation
        {
            BaseGameSource = baseSource,
            BaseGameId = baseId,
            RelatedGameSource = dto.RelatedSource,
            RelatedGameId = dto.RelatedId,
            RelationType = dto.RelationType,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GameRelations.AddAsync(relation, cancellationToken);
        await SaveChangesAsync(cancellationToken);

        return relation.Id;
    }

    public async Task RemoveRelationAsync(
        CatalogSource baseSource,
        long baseId,
        CatalogSource relatedSource,
        long relatedId,
        GameType relationType,
        CancellationToken cancellationToken = default)
    {
        baseId.EnsureValidCatalogReference(baseSource, "Əsas oyun");
        relatedId.EnsureValidCatalogReference(relatedSource, "Əlaqəli oyun");
        ValidateRelationType(relationType);

        var relation = await _context.GameRelations.FirstOrDefaultAsync(x =>
            x.BaseGameSource == baseSource &&
            x.BaseGameId == baseId &&
            x.RelatedGameSource == relatedSource &&
            x.RelatedGameId == relatedId &&
            x.RelationType == relationType,
            cancellationToken)
            ?? throw new NotFoundException("Oyun əlaqəsi tapılmadı.");

        _context.GameRelations.Remove(relation);
        await SaveChangesAsync(cancellationToken);
    }

    private async Task<RelatedGamesDTO> GetIgdbRelatedGamesAsync(
    IgdbGameModel game,
    CancellationToken cancellationToken)
    {
        var categorizedRelatedIds = game.DlcIds
            .Concat(game.ExpansionIds)
            .Concat(game.StandaloneExpansionIds)
            .Concat(game.BundleIds)
            .Concat(game.RemakeIds)
            .Concat(game.RemasterIds)
            .Concat(
                game.ParentGameId.HasValue
                    ? [game.ParentGameId.Value]
                    : Array.Empty<long>())
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        var categorizedRelatedIdSet = categorizedRelatedIds.ToHashSet();

        var seriesIds = game.SeriesGameIds
            .Where(x =>
                x > 0 &&
                x != game.Id &&
                !categorizedRelatedIdSet.Contains(x))
            .Distinct()
            .Take(200)
            .ToArray();

        var franchiseIds = game.FranchiseGameIds
            .Where(x =>
                x > 0 &&
                x != game.Id)
            .Distinct()
            .ToArray();

        var relatedIds = categorizedRelatedIds
            .Concat(seriesIds)
            .Concat(franchiseIds)
            .Distinct()
            .ToArray();

        if (relatedIds.Length == 0)
        {
            return new RelatedGamesDTO();
        }

        IReadOnlyList<IgdbGameModel> igdbGames;

        try
        {
            igdbGames = await _igdbClient.GetGamesByIdsForCardsAsync(
                relatedIds,
                cancellationToken);
        }
        catch (ExternalServiceException)
        {
            return new RelatedGamesDTO();
        }

        var mappedGames = await MapIgdbGamesAsync(
            igdbGames,
            cancellationToken);

        var gameMap = mappedGames
            .ToDictionary(
                x => x.Id,
                ToListItem);

        var igdbGameMap = igdbGames
            .ToDictionary(x => x.Id);

        List<GameListItemDTO> Resolve(
            IEnumerable<long> ids)
        {
            return ids
                .Distinct()
                .Where(gameMap.ContainsKey)
                .Select(id => gameMap[id])
                .ToList();
        }

        List<GameListItemDTO> ResolveSeries()
        {
            return seriesIds
                .Where(id =>
                    gameMap.ContainsKey(id) &&
                    igdbGameMap.TryGetValue(id, out var relatedGame) &&
                    SeriesGameTypes.Contains(relatedGame.GameType) &&
                    relatedGame.VersionParentId is null &&
                    MatchesSeriesTitle(
                        game.SeriesName,
                        relatedGame.Name))
                .Select(id => gameMap[id])
                .OrderBy(x => x.ReleaseDate is null)
                .ThenBy(x => x.ReleaseDate)
                .ThenBy(x => x.Title, GameTitleComparer)
                .Take(60)
                .ToList();
        }

        List<GameListItemDTO> ResolveFranchise()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            return franchiseIds
                .Where(id =>
                    gameMap.ContainsKey(id) &&
                    igdbGameMap.TryGetValue(id, out var relatedGame) &&
                    FranchiseGameTypes.Contains(relatedGame.GameType) &&
                    relatedGame.VersionParentId is null &&
                    relatedGame.ReleaseDate.HasValue &&
                    relatedGame.ReleaseDate.Value <= today)
                .Select(id => gameMap[id])
                .OrderBy(x => x.ReleaseDate)
                .ThenBy(x => x.Title, GameTitleComparer)
                .ToList();
        }

        return new RelatedGamesDTO
        {
            ParentGame =
                game.ParentGameId.HasValue &&
                gameMap.TryGetValue(
                    game.ParentGameId.Value,
                    out var parent)
                        ? parent
                        : null,

            SeriesGames =
                ResolveSeries(),

            FranchiseGames =
                ResolveFranchise(),

            Dlcs =
                Resolve(game.DlcIds),

            Expansions =
                Resolve(game.ExpansionIds),

            StandaloneExpansions =
                Resolve(game.StandaloneExpansionIds),

            Bundles =
                Resolve(game.BundleIds),

            Remakes =
                Resolve(game.RemakeIds),

            Remasters =
                Resolve(game.RemasterIds)
        };
    }
    private async Task<RelatedGamesDTO> GetManualRelatedGamesAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        var outgoingRelations = await _context.GameRelations
            .AsNoTracking()
            .Where(x =>
                x.BaseGameSource == source &&
                x.BaseGameId == id)
            .ToListAsync(cancellationToken);

        var incomingRelations = await _context.GameRelations
            .AsNoTracking()
            .Where(x =>
                x.RelatedGameSource == source &&
                x.RelatedGameId == id)
            .ToListAsync(cancellationToken);

        if (outgoingRelations.Count == 0 && incomingRelations.Count == 0)
            return new RelatedGamesDTO();

        var references = outgoingRelations
            .Select(x => new CatalogReferenceDTO
            {
                Source = x.RelatedGameSource,
                Id = x.RelatedGameId
            })
            .Concat(incomingRelations
                .Where(x => ParentRelationTypes.Contains(x.RelationType))
                .Select(x => new CatalogReferenceDTO
                {
                    Source = x.BaseGameSource,
                    Id = x.BaseGameId
                }))
            .DistinctBy(x => new { x.Source, x.Id })
            .ToList();

        var gameMap = await ResolveRelationGameReferencesAsync(
            references,
            cancellationToken);

        GameListItemDTO? Resolve(CatalogSource gameSource, long gameId)
        {
            return gameMap.TryGetValue((gameSource, gameId), out var game)
                ? ToListItem(game)
                : null;
        }

        List<GameListItemDTO> ResolveOutgoing(GameType relationType)
        {
            return outgoingRelations
                .Where(x => x.RelationType == relationType)
                .Select(x => Resolve(x.RelatedGameSource, x.RelatedGameId))
                .Where(x => x is not null)
                .Select(x => x!)
                .DistinctBy(x => new { x.Source, x.Id })
                .ToList();
        }

        var parentGame = incomingRelations
            .Where(x => ParentRelationTypes.Contains(x.RelationType))
            .Select(x => Resolve(x.BaseGameSource, x.BaseGameId))
            .FirstOrDefault(x => x is not null);

        return new RelatedGamesDTO
        {
            ParentGame = parentGame,
            Dlcs = ResolveOutgoing(GameType.DlcAddon),
            Expansions = ResolveOutgoing(GameType.Expansion),
            StandaloneExpansions = ResolveOutgoing(GameType.StandaloneExpansion),
            Bundles = ResolveOutgoing(GameType.Bundle),
            Remakes = ResolveOutgoing(GameType.Remake),
            Remasters = ResolveOutgoing(GameType.Remaster)
        };
    }

    private async Task<IReadOnlyDictionary<(CatalogSource Source, long Id), GameDetailDTO>>
        ResolveRelationGameReferencesAsync(
            IReadOnlyCollection<CatalogReferenceDTO> references,
            CancellationToken cancellationToken)
    {
        var result = new Dictionary<(CatalogSource Source, long Id), GameDetailDTO>();

        var localReferences = references
            .Where(x => x.Source == CatalogSource.Local)
            .ToList();

        if (localReferences.Count > 0)
        {
            var locals = await GetByReferencesAsync(localReferences, cancellationToken);

            foreach (var item in locals)
                result[item.Key] = item.Value;
        }

        var igdbReferences = references
            .Where(x => x.Source == CatalogSource.Igdb)
            .ToList();

        if (igdbReferences.Count > 0 && _igdbClient.IsEnabled)
        {
            try
            {
                var igdbGames = await GetByReferencesAsync(
                    igdbReferences,
                    cancellationToken);

                foreach (var item in igdbGames)
                    result[item.Key] = item.Value;
            }
            catch (ExternalServiceException)
            {
                // Local oyun səhifəsi IGDB müvəqqəti əlçatmaz olduqda da açılmalıdır.
            }
        }

        return result;
    }

    private static RelatedGamesDTO MergeRelatedGames(
        RelatedGamesDTO first,
        RelatedGamesDTO second)
    {
        return new RelatedGamesDTO
        {
            ParentGame = first.ParentGame ?? second.ParentGame,
            SeriesGames = MergeRelatedList(
                first.SeriesGames,
                second.SeriesGames),
            FranchiseGames = MergeRelatedList(
                first.FranchiseGames,
                second.FranchiseGames),
            Dlcs = MergeRelatedList(first.Dlcs, second.Dlcs),
            Expansions = MergeRelatedList(first.Expansions, second.Expansions),
            StandaloneExpansions = MergeRelatedList(
                first.StandaloneExpansions,
                second.StandaloneExpansions),
            Bundles = MergeRelatedList(first.Bundles, second.Bundles),
            Remakes = MergeRelatedList(first.Remakes, second.Remakes),
            Remasters = MergeRelatedList(first.Remasters, second.Remasters)
        };
    }

    private static bool MatchesSeriesTitle(
        string? seriesName,
        string gameTitle)
    {
        if (string.IsNullOrWhiteSpace(seriesName))
            return true;

        var seriesKey = seriesName
            .Split(
                [' ', '-', '_', ':', '.', ',', '/', '\\', '(', ')', '[', ']'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeSearchText)
            .LastOrDefault(token =>
                token.Length >= 3 &&
                token is not "series" and
                    not "collection" and
                    not "franchise");

        return string.IsNullOrWhiteSpace(seriesKey) ||
               NormalizeSearchText(gameTitle).Contains(
                   seriesKey,
                   StringComparison.Ordinal);
    }

    private static List<GameListItemDTO> MergeRelatedList(
        IEnumerable<GameListItemDTO> first,
        IEnumerable<GameListItemDTO> second)
    {
        return first
            .Concat(second)
            .DistinctBy(x => new { x.Source, x.Id })
            .ToList();
    }

    private static void ValidateRelationType(GameType relationType)
    {
        relationType.EnsureDefined("Əlaqə tipi");

        if (!AllowedRelationTypes.Contains(relationType))
        {
            throw new BadRequestException(
                "Əlaqə tipi DLC, expansion, standalone expansion, bundle, remake və ya remaster olmalıdır.");
        }
    }

    public async Task<long> CreateAsync(
        GameCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        dto.GameType.EnsureDefined("Oyun tipi");

        var title = dto.Title.NormalizeRequiredText("Oyunun adı", 200);
        var genreReferences = NormalizeCatalogReferences(dto.GenreReferences, "Janr");
        var platformReferences = NormalizeCatalogReferences(dto.PlatformReferences, "Platforma");
        var companyReferences = NormalizeCompanyReferences(dto.CompanyReferences);

        await ValidateGenreReferencesAsync(genreReferences, cancellationToken);
        await ValidatePlatformReferencesAsync(platformReferences, cancellationToken);
        await ValidateCompanyReferencesAsync(companyReferences, cancellationToken);

        var game = new Game
        {
            Name = title,
            GameType = dto.GameType,
            Slug = await CreateUniqueSlugAsync(title, cancellationToken: cancellationToken),
            Description = dto.Description.NormalizeOptionalText("Oyunun açıqlaması", 4000),
            ReleaseDate = dto.ReleaseDate,
            CoverImageUrl = dto.CoverImageUrl.NormalizeOptionalText("Şəkil linki", 2048),
            CreatedAt = DateTime.UtcNow,
            GameGenres = genreReferences.Select(x => new GameGenre
            {
                GenreSource = x.Source,
                GenreSourceId = x.Id
            }).ToList(),
            GamePlatforms = platformReferences.Select(x => new GamePlatform
            {
                PlatformSource = x.Source,
                PlatformSourceId = x.Id
            }).ToList(),
            GameCompanies = companyReferences.Select(x => new GameCompany
            {
                CompanySource = x.Source,
                CompanySourceId = x.Id,
                Role = x.Role
            }).ToList()
        };

        await _context.Games.AddAsync(game, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return game.Id;
    }

    public async Task UpdateAsync(
        CatalogSource source,
        long id,
        GameUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Oyun");
        ArgumentNullException.ThrowIfNull(dto);

        var hasChanges = dto.GameType.HasValue ||
                         dto.Title is not null ||
                         dto.Description is not null ||
                         dto.CoverImageUrl is not null ||
                         dto.ReleaseDate.HasValue ||
                         dto.GenreReferences is not null ||
                         dto.PlatformReferences is not null ||
                         dto.CompanyReferences is not null;

        if (dto.GameType.HasValue)
            dto.GameType.Value.EnsureDefined("Oyun tipi");

        var genreReferences = dto.GenreReferences is null
            ? null
            : NormalizeCatalogReferences(dto.GenreReferences, "Janr");

        var platformReferences = dto.PlatformReferences is null
            ? null
            : NormalizeCatalogReferences(dto.PlatformReferences, "Platforma");

        var companyReferences = dto.CompanyReferences is null
            ? null
            : NormalizeCompanyReferences(dto.CompanyReferences);

        if (genreReferences is not null)
        {
            await ValidateGenreReferencesAsync(genreReferences, cancellationToken);
        }

        if (platformReferences is not null)
        {
            await ValidatePlatformReferencesAsync(platformReferences, cancellationToken);
        }

        if (companyReferences is not null)
        {
            await ValidateCompanyReferencesAsync(companyReferences, cancellationToken);
        }

        if (source == CatalogSource.Local)
        {
            if (id > int.MaxValue)
                throw new NotFoundException("Oyun tapılmadı.");

            var game = await _context.Games
                .AsSplitQuery()
                .Include(x => x.GameGenres)
                .Include(x => x.GamePlatforms)
                .Include(x => x.GameCompanies)
                .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                ?? throw new NotFoundException("Oyun tapılmadı.");

            if (!hasChanges) return;


            if (dto.GameType.HasValue)
                game.GameType = dto.GameType.Value;

            if (dto.Title is not null)
                game.Name = dto.Title.NormalizeRequiredText("Oyunun adı", 200);

            if (dto.Description is not null)
                game.Description = dto.Description.NormalizeOptionalText("Oyunun açıqlaması", 4000);

            if (dto.CoverImageUrl is not null)
                game.CoverImageUrl = dto.CoverImageUrl.NormalizeOptionalText("Şəkil linki", 2048);

            if (dto.ReleaseDate.HasValue)
            {
                game.ReleaseDate = dto.ReleaseDate;
            }

            if (genreReferences is not null)
            {
                SyncLocalGenres(game, genreReferences);
            }

            if (platformReferences is not null)
            {
                SyncLocalPlatforms(game, platformReferences);
            }

            if (companyReferences is not null)
            {
                SyncLocalCompanies(game, companyReferences);
            }

            game.UpdatedAt = DateTime.UtcNow;

            await UpdateGameListSnapshotsAsync(
                CatalogSource.Local,
                id,
                game.Name,
                game.CoverImageUrl,
                game.ReleaseDate,
                game.AverageRating,
                cancellationToken);
        }
        else
        {
            var igdbGame = await _igdbClient.GetGameByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("IGDB oyunu tapılmadı.");

            if (!hasChanges) return;


            var gameOverride = await _context.IgdbGameOverrides
                .AsSplitQuery()
                .Include(x => x.Genres)
                .Include(x => x.Platforms)
                .Include(x => x.Companies)
                .FirstOrDefaultAsync(x => x.IgdbGameId == id, cancellationToken);

            if (gameOverride is null)
            {
                gameOverride = new IgdbGameOverride
                {
                    IgdbGameId = id,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.IgdbGameOverrides.AddAsync(gameOverride, cancellationToken);
            }

            if (dto.GameType.HasValue)
                gameOverride.GameType = dto.GameType.Value;

            if (dto.Title is not null)
                gameOverride.Name = dto.Title.NormalizeRequiredText("Oyunun adı", 200);

            if (dto.Description is not null)
                gameOverride.Description = dto.Description.NormalizeOptionalText("Oyunun açıqlaması", 4000);

            if (dto.CoverImageUrl is not null)
                gameOverride.CoverImageUrl = dto.CoverImageUrl.NormalizeOptionalText("Şəkil linki", 2048);

            if (dto.ReleaseDate.HasValue)
                gameOverride.ReleaseDate = dto.ReleaseDate;

            if (genreReferences is not null)
                SyncIgdbOverrideGenres(gameOverride, genreReferences);

            if (platformReferences is not null)
                SyncIgdbOverridePlatforms(gameOverride, platformReferences);

            if (companyReferences is not null)
                SyncIgdbOverrideCompanies(gameOverride, companyReferences);

            gameOverride.IsHidden = false;
            gameOverride.UpdatedAt = DateTime.UtcNow;

            await UpdateGameListSnapshotsAsync(
                CatalogSource.Igdb,
                id,
                gameOverride.Name ?? igdbGame.Name,
                gameOverride.CoverImageUrl ?? igdbGame.CoverImageUrl,
                gameOverride.ReleaseDate ?? igdbGame.ReleaseDate,
                gameOverride.Rating ?? igdbGame.Rating,
                cancellationToken);
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Oyun");

        if (source == CatalogSource.Local)
        {
            if (id > int.MaxValue)
                throw new NotFoundException("Oyun tapılmadı.");

            var game = await _context.Games
                .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                ?? throw new NotFoundException("Oyun tapılmadı.");

            game.IsDeleted = true;
            game.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _ = await _igdbClient.GetGameByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("IGDB oyunu tapılmadı.");

            var gameOverride = await _context.IgdbGameOverrides
                .FirstOrDefaultAsync(x => x.IgdbGameId == id, cancellationToken);

            if (gameOverride is null)
            {
                gameOverride = new IgdbGameOverride
                {
                    IgdbGameId = id,
                    IsHidden = true,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.IgdbGameOverrides.AddAsync(gameOverride, cancellationToken);
            }
            else
            {
                gameOverride.IsHidden = true;
                gameOverride.UpdatedAt = DateTime.UtcNow;
            }
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Oyun");

        if (source == CatalogSource.Local)
        {
            if (id > int.MaxValue)
                throw new NotFoundException("Oyun tapılmadı.");

            var game = await _context.Games
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                ?? throw new NotFoundException("Oyun tapılmadı.");

            if (!game.IsDeleted)
                return;

            game.IsDeleted = false;
            game.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _ = await _igdbClient.GetGameByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("IGDB oyunu tapılmadı.");

            var gameOverride = await _context.IgdbGameOverrides
                .FirstOrDefaultAsync(x => x.IgdbGameId == id, cancellationToken)
                ?? throw new NotFoundException("Gizlədilmiş IGDB oyunu tapılmadı.");

            if (!gameOverride.IsHidden)
                throw new ConflictException("IGDB oyunu gizlədilməyib.");

            gameOverride.IsHidden = false;
            gameOverride.UpdatedAt = DateTime.UtcNow;
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<(CatalogSource Source, long Id), GameDetailDTO>> GetByReferencesAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(references);

        foreach (var reference in references)
        {
            if (reference is null)
                throw new BadRequestException("Oyun istinadı boş ola bilməz.");

            reference.Id.EnsureValidCatalogReference(reference.Source, "Oyun");
        }

        var normalized = references
            .DistinctBy(x => new { x.Source, x.Id })
            .ToList();

        var result = new Dictionary<(CatalogSource Source, long Id), GameDetailDTO>();

        var localIds = normalized
            .Where(x => x.Source == CatalogSource.Local && x.Id <= int.MaxValue)
            .Select(x => (int)x.Id)
            .ToArray();

        if (localIds.Length > 0)
        {
            var localGames = await _context.Games
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.GameGenres)
                .Include(x => x.GamePlatforms)
                .Include(x => x.GameCompanies)
                .Where(x => localIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var game in await MapLocalGamesAsync(localGames, cancellationToken))
                result[(game.Source, game.Id)] = game;
        }

        var igdbIds = normalized
            .Where(x => x.Source == CatalogSource.Igdb)
            .Select(x => x.Id)
            .ToArray();

        if (igdbIds.Length > 0)
        {
            var igdbGames = await _igdbClient.GetGamesByIdsAsync(igdbIds, cancellationToken);

            foreach (var game in await MapIgdbGamesAsync(igdbGames, cancellationToken))
                result[(game.Source, game.Id)] = game;
        }

        return result;
    }

    private IQueryable<Game> BuildLocalQuery(GameFilterDTO filter)
    {
        var query = _context.Games.AsNoTracking();

        if (!filter.IncludeRelatedContent)
            query = query.Where(x => MainCatalogGameTypes.Contains(x.GameType));

        if (!string.IsNullOrWhiteSpace(filter.SearchName))
        {
            var search = filter.SearchName.Trim();
            query = query.Where(x => x.Name.Contains(search));
        }

        if (filter.CompanyId.HasValue)
        {
            query = query.Where(x => x.GameCompanies.Any(company =>
                company.CompanySource == filter.CompanySource!.Value &&
                company.CompanySourceId == filter.CompanyId.Value));
        }

        if (filter.GenreId.HasValue)
        {
            query = query.Where(x => x.GameGenres.Any(genre =>
                genre.GenreSource == filter.GenreSource!.Value &&
                genre.GenreSourceId == filter.GenreId.Value));
        }

        if (filter.PlatformId.HasValue)
        {
            query = query.Where(x => x.GamePlatforms.Any(platform =>
                platform.PlatformSource == filter.PlatformSource!.Value &&
                platform.PlatformSourceId == filter.PlatformId.Value));
        }

        if (filter.ReleaseYear.HasValue)
        {
            query = query.Where(x =>
                x.ReleaseDate.HasValue &&
                x.ReleaseDate.Value.Year == filter.ReleaseYear.Value);
        }

        if (filter.ReleasedOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(x =>
                x.ReleaseDate.HasValue &&
                x.ReleaseDate.Value <= today);
        }

        return query;
    }

    private static IQueryable<Game> ApplyLocalSort(
        IQueryable<Game> query,
        string? sortBy,
        string? searchName = null)
    {
        var normalizedSort = sortBy?.Trim().ToLowerInvariant();

        if (
            string.IsNullOrWhiteSpace(normalizedSort) &&
            !string.IsNullOrWhiteSpace(searchName))
        {
            var search = searchName.Trim();

            return query
                .OrderByDescending(game => game.Name == search)
                .ThenByDescending(game => game.Name.StartsWith(search))
                .ThenBy(game => game.Name.Length)
                .ThenBy(game => game.Name)
                .ThenBy(game => game.Id);
        }

        return normalizedSort switch
        {
            "name_asc" => query
                .OrderByDescending(game =>
                    EF.Functions.Like(
                        game.Name,
                        SqlLetterStartPattern))
                .ThenBy(game => game.Name)
                .ThenBy(game => game.Id),
            "name_desc" => query
                .OrderByDescending(game =>
                    EF.Functions.Like(
                        game.Name,
                        SqlLetterStartPattern))
                .ThenByDescending(game => game.Name)
                .ThenBy(game => game.Id),
            "rating_asc" => query
                .OrderBy(x => x.AverageRating)
                .ThenBy(x => x.Id),
            "rating_desc" => query
                .OrderByDescending(x => x.AverageRating)
                .ThenBy(x => x.Id),
            "play_asc" => query
                .OrderBy(x => x.PlayCount)
                .ThenBy(x => x.Id),
            "play_desc" => query
                .OrderByDescending(x => x.PlayCount)
                .ThenBy(x => x.Id),
            "year_asc" => query
                .OrderBy(x => x.ReleaseDate)
                .ThenBy(x => x.Id),
            "year_desc" => query
                .OrderByDescending(x => x.ReleaseDate)
                .ThenBy(x => x.Id),
            _ => query
                .OrderByDescending(x => x.ReleaseDate)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.Id)
        };
    }

    private static IEnumerable<GameDetailDTO> SortGames(
        IEnumerable<GameDetailDTO> games,
        string? sortBy,
        string? searchName = null)
    {
        var normalizedSort = sortBy?.Trim().ToLowerInvariant();

        if (
            string.IsNullOrWhiteSpace(normalizedSort) &&
            !string.IsNullOrWhiteSpace(searchName))
        {
            return games
                .OrderByDescending(game =>
                    CalculateSearchRelevance(game.Title, searchName))
                .ThenByDescending(game => game.AverageRating ?? double.MinValue)
                .ThenByDescending(game => game.ReleaseDate ?? DateOnly.MinValue)
                .ThenBy(game => game.Title, GameTitleComparer)
                .ThenBy(game => game.Source)
                .ThenBy(game => game.Id);
        }

        return normalizedSort switch
        {
            "name_asc" => games
                .OrderByDescending(game => StartsWithLetter(game.Title))
                .ThenBy(game => game.Title, GameTitleComparer)
                .ThenBy(game => game.Source)
                .ThenBy(game => game.Id),
            "name_desc" => games
                .OrderByDescending(game => StartsWithLetter(game.Title))
                .ThenByDescending(game => game.Title, GameTitleComparer)
                .ThenBy(game => game.Source)
                .ThenBy(game => game.Id),
            "rating_asc" => games
                .OrderBy(x => x.AverageRating ?? double.MaxValue)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.Id),
            "rating_desc" => games
                .OrderByDescending(x => x.AverageRating ?? double.MinValue)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.Id),
            "play_asc" => games
                .OrderBy(x => x.PlayCount)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.Id),
            "play_desc" => games
                .OrderByDescending(x => x.PlayCount)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.Id),
            "year_asc" => games
                .OrderBy(x => x.ReleaseDate ?? DateOnly.MaxValue)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.Id),
            "year_desc" => games
                .OrderByDescending(x => x.ReleaseDate ?? DateOnly.MinValue)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.Id),
            _ => games
                .OrderByDescending(x => x.ReleaseDate ?? DateOnly.MinValue)
                .ThenBy(x => x.Title, GameTitleComparer)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.Id)
        };
    }

    private static int CalculateSearchRelevance(
        string title,
        string searchText)
    {
        var normalizedTitle = NormalizeSearchText(title);
        var normalizedSearch = NormalizeSearchText(searchText);

        if (normalizedSearch.Length == 0)
        {
            return 0;
        }

        if (string.Equals(
                normalizedTitle,
                normalizedSearch,
                StringComparison.Ordinal))
        {
            return 5_000;
        }

        if (normalizedTitle.StartsWith(
                normalizedSearch,
                StringComparison.Ordinal))
        {
            return 4_000;
        }

        var normalizedWords = title
            .Split(
                [' ', '-', '_', ':', '.', ',', '/', '\\', '(', ')', '[', ']'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeSearchText)
            .Where(word => word.Length > 0)
            .ToArray();

        if (normalizedWords.Any(word =>
                word.StartsWith(
                    normalizedSearch,
                    StringComparison.Ordinal)))
        {
            return 3_500;
        }

        if (normalizedTitle.Contains(
                normalizedSearch,
                StringComparison.Ordinal))
        {
            return 3_000;
        }

        var acronym = string.Concat(
            normalizedWords
                .Where(word => word.Length > 0)
                .Select(word => word[0]));

        return acronym.StartsWith(
            normalizedSearch,
            StringComparison.Ordinal)
            ? 2_000
            : 0;
    }

    private static bool StartsWithLetter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return char.IsLetter(character);
        }

        return false;
    }

    private static bool CanQueryIgdb(GameFilterDTO filter)
    {
        return (!filter.CompanyId.HasValue || filter.CompanySource == CatalogSource.Igdb) &&
               (!filter.GenreId.HasValue || filter.GenreSource == CatalogSource.Igdb) &&
               (!filter.PlatformId.HasValue || filter.PlatformSource == CatalogSource.Igdb);
    }

    private async Task<IReadOnlyList<GameDetailDTO>> MapLocalGamesAsync(
        IReadOnlyCollection<Game> games,
        CancellationToken cancellationToken)
    {
        if (games.Count == 0)
            return Array.Empty<GameDetailDTO>();

        var genreRefs = games.SelectMany(x => x.GameGenres)
            .Select(x => new CatalogReferenceDTO { Source = x.GenreSource, Id = x.GenreSourceId })
            .ToList();

        var platformRefs = games.SelectMany(x => x.GamePlatforms)
            .Select(x => new CatalogReferenceDTO { Source = x.PlatformSource, Id = x.PlatformSourceId })
            .ToList();

        var companyRefs = games.SelectMany(x => x.GameCompanies)
            .Select(x => new CatalogReferenceDTO { Source = x.CompanySource, Id = x.CompanySourceId })
            .ToList();

        var genreMap = await ResolveGenreLookupsAsync(genreRefs, cancellationToken, true);
        var platformMap = await ResolvePlatformLookupsAsync(platformRefs, cancellationToken, true);
        var companyMap = await ResolveCompanyLookupsAsync(companyRefs, cancellationToken, true);
        var communityStatsMap = await GetCommunityStatsMapAsync(
            games.Select(game => new CatalogReferenceDTO
            {
                Source = CatalogSource.Local,
                Id = game.Id
            }).ToList(),
            cancellationToken);

        return games.Select(game =>
        {
            var communityStats = communityStatsMap.GetValueOrDefault(
                (CatalogSource.Local, game.Id)) ?? new GameCommunityStatsDTO();

            return new GameDetailDTO
            {
                Source = CatalogSource.Local,
                Id = game.Id,
                Title = game.Name,
                Description = game.Description,
                ReleaseDate = game.ReleaseDate,
                CoverImageUrl = game.CoverImageUrl,
                PlayCount = communityStats.PlayedCount,
                AverageRating = communityStats.AverageRating,
                CommunityStats = communityStats,
                IsOverridden = false,
                GameType = game.GameType,
                Genres = game.GameGenres
                .Select(x => genreMap.GetValueOrDefault((x.GenreSource, x.GenreSourceId)))
                .Where(x => x is not null)
                .Select(x => x!)
                .OrderBy(x => x.Name)
                .ToList(),
                Platforms = game.GamePlatforms
                .Select(x => platformMap.GetValueOrDefault((x.PlatformSource, x.PlatformSourceId)))
                .Where(x => x is not null)
                .Select(x => x!)
                .OrderBy(x => x.Name)
                .ToList(),
                Companies = game.GameCompanies
                .Select(x => companyMap.TryGetValue((x.CompanySource, x.CompanySourceId), out var company)
                    ? new CompanyLookupDTO
                    {
                        Source = company.Source,
                        Id = company.Id,
                        Name = company.Name,
                        IsOverridden = company.IsOverridden,
                        Role = x.Role
                    }
                    : null)
                .Where(x => x is not null)
                .Select(x => x!)
                .OrderBy(x => x.Role)
                .ThenBy(x => x.Name)
                .ToList()
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<GameDetailDTO>> MapIgdbGamesAsync(
        IReadOnlyCollection<IgdbGameModel> games,
        CancellationToken cancellationToken)
    {
        if (games.Count == 0)
            return Array.Empty<GameDetailDTO>();

        var ids = games.Select(x => x.Id).ToArray();
        var overrides = await _context.IgdbGameOverrides
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Genres)
            .Include(x => x.Platforms)
            .Include(x => x.Companies)
            .Where(x => ids.Contains(x.IgdbGameId))
            .ToDictionaryAsync(x => x.IgdbGameId, cancellationToken);

        var overrideGenreRefs = overrides.Values
            .Where(x => x.ReplaceGenres)
            .SelectMany(x => x.Genres)
            .Select(x => new CatalogReferenceDTO { Source = x.GenreSource, Id = x.GenreSourceId })
            .ToList();

        var overridePlatformRefs = overrides.Values
            .Where(x => x.ReplacePlatforms)
            .SelectMany(x => x.Platforms)
            .Select(x => new CatalogReferenceDTO { Source = x.PlatformSource, Id = x.PlatformSourceId })
            .ToList();

        var overrideCompanyRefs = overrides.Values
            .Where(x => x.ReplaceCompanies)
            .SelectMany(x => x.Companies)
            .Select(x => new CatalogReferenceDTO { Source = x.CompanySource, Id = x.CompanySourceId })
            .ToList();

        var genreMap = await ResolveGenreLookupsAsync(overrideGenreRefs, cancellationToken, true);
        var platformMap = await ResolvePlatformLookupsAsync(overridePlatformRefs, cancellationToken, true);
        var companyMap = await ResolveCompanyLookupsAsync(overrideCompanyRefs, cancellationToken, true);

        var baseGenreIds = games
            .SelectMany(x => x.Genres)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        var basePlatformIds = games
            .SelectMany(x => x.Platforms)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        var baseCompanyIds = games
            .SelectMany(x => x.Companies)
            .Select(x => x.Company.Id)
            .Distinct()
            .ToArray();

        var baseGenreOverrides = await _context.IgdbGenreOverrides
            .AsNoTracking()
            .Where(x => baseGenreIds.Contains(x.IgdbGenreId))
            .ToDictionaryAsync(x => x.IgdbGenreId, cancellationToken);

        var basePlatformOverrides = await _context.IgdbPlatformOverrides
            .AsNoTracking()
            .Where(x => basePlatformIds.Contains(x.IgdbPlatformId))
            .ToDictionaryAsync(x => x.IgdbPlatformId, cancellationToken);

        var baseCompanyOverrides = await _context.IgdbCompanyOverrides
            .AsNoTracking()
            .Where(x => baseCompanyIds.Contains(x.IgdbCompanyId))
            .ToDictionaryAsync(x => x.IgdbCompanyId, cancellationToken);

        var communityStatsMap = await GetCommunityStatsMapAsync(
            games.Select(game => new CatalogReferenceDTO
            {
                Source = CatalogSource.Igdb,
                Id = game.Id
            }).ToList(),
            cancellationToken);

        var result = new List<GameDetailDTO>();

        foreach (var game in games)
        {
            overrides.TryGetValue(game.Id, out var gameOverride);

            if (gameOverride?.IsHidden == true)
                continue;

            var communityStats = communityStatsMap.GetValueOrDefault(
                (CatalogSource.Igdb, game.Id)) ?? new GameCommunityStatsDTO();

            var genres = gameOverride?.ReplaceGenres == true
                ? gameOverride.Genres
                    .Select(x => genreMap.GetValueOrDefault((x.GenreSource, x.GenreSourceId)))
                    .Where(x => x is not null)
                    .Select(x => x!)
                    .ToList()
                : game.Genres
                    .Where(x => !baseGenreOverrides.TryGetValue(x.Id, out var itemOverride) || !itemOverride.IsHidden)
                    .Select(x => new LookupDTO
                    {
                        Source = CatalogSource.Igdb,
                        Id = x.Id,
                        Name = baseGenreOverrides.TryGetValue(x.Id, out var itemOverride) && itemOverride.Name is not null
                            ? itemOverride.Name
                            : x.Name,
                        IsOverridden = baseGenreOverrides.ContainsKey(x.Id)
                    })
                    .ToList();

            var platforms = gameOverride?.ReplacePlatforms == true
                ? gameOverride.Platforms
                    .Select(x => platformMap.GetValueOrDefault((x.PlatformSource, x.PlatformSourceId)))
                    .Where(x => x is not null)
                    .Select(x => x!)
                    .ToList()
                : game.Platforms
                    .Where(x => !basePlatformOverrides.TryGetValue(x.Id, out var itemOverride) || !itemOverride.IsHidden)
                    .Select(x => new LookupDTO
                    {
                        Source = CatalogSource.Igdb,
                        Id = x.Id,
                        Name = basePlatformOverrides.TryGetValue(x.Id, out var itemOverride) && itemOverride.Name is not null
                            ? itemOverride.Name
                            : x.Name,
                        IsOverridden = basePlatformOverrides.ContainsKey(x.Id)
                    })
                    .ToList();

            var companies = gameOverride?.ReplaceCompanies == true
                ? gameOverride.Companies
                    .Select(x => companyMap.TryGetValue((x.CompanySource, x.CompanySourceId), out var company)
                        ? new CompanyLookupDTO
                        {
                            Source = company.Source,
                            Id = company.Id,
                            Name = company.Name,
                            IsOverridden = company.IsOverridden,
                            Role = x.Role
                        }
                        : null)
                    .Where(x => x is not null)
                    .Select(x => x!)
                    .ToList()
                : game.Companies
                    .Where(x => !baseCompanyOverrides.TryGetValue(x.Company.Id, out var itemOverride) || !itemOverride.IsHidden)
                    .Select(x => new CompanyLookupDTO
                    {
                        Source = CatalogSource.Igdb,
                        Id = x.Company.Id,
                        Name = baseCompanyOverrides.TryGetValue(x.Company.Id, out var itemOverride) && itemOverride.Name is not null
                            ? itemOverride.Name
                            : x.Company.Name,
                        IsOverridden = baseCompanyOverrides.ContainsKey(x.Company.Id),
                        Role = x.Role
                    })
                    .ToList();

            result.Add(new GameDetailDTO
            {
                Source = CatalogSource.Igdb,
                Id = game.Id,
                Title = gameOverride?.Name ?? game.Name,
                Description = gameOverride?.Description ?? game.Description,
                ReleaseDate = gameOverride?.ReleaseDate ?? game.ReleaseDate,
                CoverImageUrl = gameOverride?.CoverImageUrl ?? game.CoverImageUrl,
                PlayCount = communityStats.PlayedCount,
                AverageRating = communityStats.AverageRating,
                CommunityStats = communityStats,
                IsOverridden = gameOverride is not null,
                GameType = gameOverride?.GameType ?? game.GameType,
                Genres = genres.OrderBy(x => x.Name).ToList(),
                Platforms = platforms.OrderBy(x => x.Name).ToList(),
                Companies = companies.OrderBy(x => x.Role).ThenBy(x => x.Name).ToList()
            });
        }

        return result;
    }

    private async Task<Dictionary<(CatalogSource Source, long Id), GameCommunityStatsDTO>>
        GetCommunityStatsMapAsync(
            IReadOnlyCollection<CatalogReferenceDTO> references,
            CancellationToken cancellationToken)
    {
        var normalizedReferences = references
            .Where(reference => reference.Id > 0)
            .DistinctBy(reference => new
            {
                reference.Source,
                reference.Id
            })
            .ToList();

        if (normalizedReferences.Count == 0)
        {
            return [];
        }

        var localIds = normalizedReferences
            .Where(reference =>
                reference.Source == CatalogSource.Local)
            .Select(reference => reference.Id)
            .ToArray();

        var igdbIds = normalizedReferences
            .Where(reference =>
                reference.Source == CatalogSource.Igdb)
            .Select(reference => reference.Id)
            .ToArray();

        var userGamesQuery = _context.UserGames
            .AsNoTracking()
            .Where(userGame =>
                (userGame.Source == CatalogSource.Local &&
                 localIds.Contains(userGame.GameId)) ||
                (userGame.Source == CatalogSource.Igdb &&
                 igdbIds.Contains(userGame.GameId)));

        var summaryRows = await userGamesQuery
            .GroupBy(userGame => new
            {
                userGame.Source,
                userGame.GameId
            })
            .Select(group => new
            {
                group.Key.Source,
                group.Key.GameId,
                AverageRating = group.Average(
                    userGame => userGame.Rating),
                RatingCount = group.Count(
                    userGame => userGame.Rating.HasValue),
                PlayingCount = group.Count(
                    userGame =>
                        userGame.Status == UserGameStatus.Playing),
                PlanToPlayCount = group.Count(
                    userGame =>
                        userGame.Status == UserGameStatus.PlanToPlay),
                PlayedCount = group.Count(
                    userGame =>
                        userGame.Status == UserGameStatus.Played),
                DroppedCount = group.Count(
                    userGame =>
                        userGame.Status == UserGameStatus.Dropped),
                WishlistCount = group.Count(
                    userGame => userGame.IsWishlisted),
                FavoriteCount = group.Count(
                    userGame => userGame.IsFavorite)
            })
            .ToListAsync(cancellationToken);

        var ratingRows = await userGamesQuery
            .Where(userGame => userGame.Rating.HasValue)
            .GroupBy(userGame => new
            {
                userGame.Source,
                userGame.GameId,
                Rating = userGame.Rating!.Value
            })
            .Select(group => new
            {
                group.Key.Source,
                group.Key.GameId,
                group.Key.Rating,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var summaryMap = summaryRows.ToDictionary(
            row => (row.Source, row.GameId));

        var ratingCountMap = ratingRows.ToDictionary(
            row =>
                (row.Source, row.GameId, row.Rating),
            row => row.Count);

        return normalizedReferences.ToDictionary(
            reference =>
                (reference.Source, reference.Id),
            reference =>
            {
                summaryMap.TryGetValue(
                    (reference.Source, reference.Id),
                    out var summary);

                var distribution = Enumerable
                    .Range(1, 10)
                    .Reverse()
                    .Select(step =>
                    {
                        var rating = step / 2d;

                        return new GameRatingDistributionItemDTO
                        {
                            Rating = rating,
                            Count = ratingCountMap.GetValueOrDefault(
                                (
                                    reference.Source,
                                    reference.Id,
                                    rating
                                ))
                        };
                    })
                    .ToList();

                return new GameCommunityStatsDTO
                {
                    AverageRating = summary?.AverageRating,
                    RatingCount = summary?.RatingCount ?? 0,
                    RatingDistribution = distribution,
                    PlayingCount = summary?.PlayingCount ?? 0,
                    PlanToPlayCount = summary?.PlanToPlayCount ?? 0,
                    PlayedCount = summary?.PlayedCount ?? 0,
                    DroppedCount = summary?.DroppedCount ?? 0,
                    WishlistCount = summary?.WishlistCount ?? 0,
                    FavoriteCount = summary?.FavoriteCount ?? 0
                };
            });
    }

    private async Task<Dictionary<(CatalogSource Source, long Id), LookupDTO>> ResolveGenreLookupsAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken,
        bool allowIgdbFallback = false)
    {
        var result = new Dictionary<(CatalogSource Source, long Id), LookupDTO>();
        var localIds = references
            .Where(x => x.Source == CatalogSource.Local)
            .Select(x => (int)x.Id)
            .Distinct()
            .ToArray();

        var locals = await _context.Genres
            .AsNoTracking()
            .Where(x => localIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var item in locals)
        {
            result[(CatalogSource.Local, item.Id)] = new LookupDTO
            {
                Source = CatalogSource.Local,
                Id = item.Id,
                Name = item.Name,
                IsOverridden = false
            };
        }

        var igdbIds = references
            .Where(x => x.Source == CatalogSource.Igdb)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        if (igdbIds.Length == 0)
            return result;

        var overrides = await _context.IgdbGenreOverrides
            .AsNoTracking()
            .Where(x => igdbIds.Contains(x.IgdbGenreId))
            .ToDictionaryAsync(x => x.IgdbGenreId, cancellationToken);

        IReadOnlyList<IgdbGenreModel> items;

        try
        {
            items = await _igdbClient.GetGenresByIdsAsync(igdbIds, cancellationToken);
        }
        catch (ExternalServiceException) when (allowIgdbFallback)
        {
            items = Array.Empty<IgdbGenreModel>();
        }

        foreach (var item in items)
        {
            overrides.TryGetValue(item.Id, out var itemOverride);

            if (itemOverride?.IsHidden == true)
                continue;

            result[(CatalogSource.Igdb, item.Id)] = new LookupDTO
            {
                Source = CatalogSource.Igdb,
                Id = item.Id,
                Name = itemOverride?.Name ?? item.Name,
                IsOverridden = itemOverride is not null
            };
        }

        AddFallbackLookups(
            result,
            igdbIds,
            overrides.ToDictionary(
                x => x.Key,
                x => (x.Value.Name, x.Value.IsHidden)),
            allowIgdbFallback);

        return result;
    }

    private async Task<Dictionary<(CatalogSource Source, long Id), LookupDTO>> ResolvePlatformLookupsAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken,
        bool allowIgdbFallback = false)
    {
        var result = new Dictionary<(CatalogSource Source, long Id), LookupDTO>();
        var localIds = references
            .Where(x => x.Source == CatalogSource.Local)
            .Select(x => (int)x.Id)
            .Distinct()
            .ToArray();

        var locals = await _context.Platforms
            .AsNoTracking()
            .Where(x => localIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var item in locals)
        {
            result[(CatalogSource.Local, item.Id)] = new LookupDTO
            {
                Source = CatalogSource.Local,
                Id = item.Id,
                Name = item.Name,
                IsOverridden = false
            };
        }

        var igdbIds = references
            .Where(x => x.Source == CatalogSource.Igdb)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        if (igdbIds.Length == 0)
            return result;

        var overrides = await _context.IgdbPlatformOverrides
            .AsNoTracking()
            .Where(x => igdbIds.Contains(x.IgdbPlatformId))
            .ToDictionaryAsync(x => x.IgdbPlatformId, cancellationToken);

        IReadOnlyList<IgdbPlatformModel> items;

        try
        {
            items = await _igdbClient.GetPlatformsByIdsAsync(igdbIds, cancellationToken);
        }
        catch (ExternalServiceException) when (allowIgdbFallback)
        {
            items = Array.Empty<IgdbPlatformModel>();
        }

        foreach (var item in items)
        {
            overrides.TryGetValue(item.Id, out var itemOverride);

            if (itemOverride?.IsHidden == true)
                continue;

            result[(CatalogSource.Igdb, item.Id)] = new LookupDTO
            {
                Source = CatalogSource.Igdb,
                Id = item.Id,
                Name = itemOverride?.Name ?? item.Name,
                IsOverridden = itemOverride is not null
            };
        }

        AddFallbackLookups(
            result,
            igdbIds,
            overrides.ToDictionary(
                x => x.Key,
                x => (x.Value.Name, x.Value.IsHidden)),
            allowIgdbFallback);

        return result;
    }

    private async Task<Dictionary<(CatalogSource Source, long Id), LookupDTO>> ResolveCompanyLookupsAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken,
        bool allowIgdbFallback = false)
    {
        var result = new Dictionary<(CatalogSource Source, long Id), LookupDTO>();
        var localIds = references
            .Where(x => x.Source == CatalogSource.Local)
            .Select(x => (int)x.Id)
            .Distinct()
            .ToArray();

        var locals = await _context.Companies
            .AsNoTracking()
            .Where(x => localIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var item in locals)
        {
            result[(CatalogSource.Local, item.Id)] = new LookupDTO
            {
                Source = CatalogSource.Local,
                Id = item.Id,
                Name = item.Name,
                IsOverridden = false
            };
        }

        var igdbIds = references
            .Where(x => x.Source == CatalogSource.Igdb)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        if (igdbIds.Length == 0)
            return result;

        var overrides = await _context.IgdbCompanyOverrides
            .AsNoTracking()
            .Where(x => igdbIds.Contains(x.IgdbCompanyId))
            .ToDictionaryAsync(x => x.IgdbCompanyId, cancellationToken);

        IReadOnlyList<IgdbCompanyModel> items;

        try
        {
            items = await _igdbClient.GetCompaniesByIdsAsync(igdbIds, cancellationToken);
        }
        catch (ExternalServiceException) when (allowIgdbFallback)
        {
            items = Array.Empty<IgdbCompanyModel>();
        }

        foreach (var item in items)
        {
            overrides.TryGetValue(item.Id, out var itemOverride);

            if (itemOverride?.IsHidden == true)
                continue;

            result[(CatalogSource.Igdb, item.Id)] = new LookupDTO
            {
                Source = CatalogSource.Igdb,
                Id = item.Id,
                Name = itemOverride?.Name ?? item.Name,
                IsOverridden = itemOverride is not null
            };
        }

        AddFallbackLookups(
            result,
            igdbIds,
            overrides.ToDictionary(
                x => x.Key,
                x => (x.Value.Name, x.Value.IsHidden)),
            allowIgdbFallback);

        return result;
    }

    private static void AddFallbackLookups(
        IDictionary<(CatalogSource Source, long Id), LookupDTO> result,
        IEnumerable<long> igdbIds,
        IReadOnlyDictionary<long, (string? Name, bool IsHidden)> overrides,
        bool allowFallback)
    {
        if (!allowFallback)
            return;

        foreach (var id in igdbIds)
        {
            if (result.ContainsKey((CatalogSource.Igdb, id)))
                continue;

            overrides.TryGetValue(id, out var itemOverride);

            if (itemOverride.IsHidden)
                continue;

            result[(CatalogSource.Igdb, id)] = new LookupDTO
            {
                Source = CatalogSource.Igdb,
                Id = id,
                Name = itemOverride.Name ?? $"IGDB #{id}",
                IsOverridden = overrides.ContainsKey(id)
            };
        }
    }

    private async Task ValidateGenreReferencesAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken)
    {
        var map = await ResolveGenreLookupsAsync(references, cancellationToken);
        EnsureAllReferencesResolved(references, map.Keys, "Janr");
    }

    private async Task ValidatePlatformReferencesAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken)
    {
        var map = await ResolvePlatformLookupsAsync(references, cancellationToken);
        EnsureAllReferencesResolved(references, map.Keys, "Platforma");
    }

    private async Task ValidateCompanyReferencesAsync(
        IReadOnlyCollection<CompanyReferenceDTO> references,
        CancellationToken cancellationToken)
    {
        var catalogReferences = references
            .Select(x => new CatalogReferenceDTO { Source = x.Source, Id = x.Id })
            .ToList();

        var map = await ResolveCompanyLookupsAsync(catalogReferences, cancellationToken);
        EnsureAllReferencesResolved(catalogReferences, map.Keys, "Şirkət");
    }

    private static void EnsureAllReferencesResolved(
        IReadOnlyCollection<CatalogReferenceDTO> requested,
        IEnumerable<(CatalogSource Source, long Id)> resolved,
        string fieldName)
    {
        var resolvedSet = resolved.ToHashSet();
        var missing = requested
            .Where(x => !resolvedSet.Contains((x.Source, x.Id)))
            .Select(x => $"{x.Source}:{x.Id}")
            .ToList();

        if (missing.Count > 0)
            throw new BadRequestException($"Bu {fieldName.ToLower()} istinadları tapılmadı: {string.Join(", ", missing)}");
    }

    private void SyncLocalGenres(
        Game game,
        IReadOnlyCollection<CatalogReferenceDTO> references)
    {
        var desired = references
            .Select(x => (x.Source, x.Id))
            .ToHashSet();

        var removed = game.GameGenres
            .Where(x => !desired.Contains((x.GenreSource, x.GenreSourceId)))
            .ToList();

        _context.GameGenres.RemoveRange(removed);

        var existing = game.GameGenres
            .Except(removed)
            .Select(x => (x.GenreSource, x.GenreSourceId))
            .ToHashSet();

        foreach (var reference in references.Where(x => !existing.Contains((x.Source, x.Id))))
        {
            game.GameGenres.Add(new GameGenre
            {
                GameId = game.Id,
                GenreSource = reference.Source,
                GenreSourceId = reference.Id
            });
        }
    }

    private void SyncLocalPlatforms(
        Game game,
        IReadOnlyCollection<CatalogReferenceDTO> references)
    {
        var desired = references
            .Select(x => (x.Source, x.Id))
            .ToHashSet();

        var removed = game.GamePlatforms
            .Where(x => !desired.Contains((x.PlatformSource, x.PlatformSourceId)))
            .ToList();

        _context.GamePlatforms.RemoveRange(removed);

        var existing = game.GamePlatforms
            .Except(removed)
            .Select(x => (x.PlatformSource, x.PlatformSourceId))
            .ToHashSet();

        foreach (var reference in references.Where(x => !existing.Contains((x.Source, x.Id))))
        {
            game.GamePlatforms.Add(new GamePlatform
            {
                GameId = game.Id,
                PlatformSource = reference.Source,
                PlatformSourceId = reference.Id
            });
        }
    }

    private void SyncLocalCompanies(
        Game game,
        IReadOnlyCollection<CompanyReferenceDTO> references)
    {
        var desired = references
            .Select(x => (x.Source, x.Id, x.Role))
            .ToHashSet();

        var removed = game.GameCompanies
            .Where(x => !desired.Contains((x.CompanySource, x.CompanySourceId, x.Role)))
            .ToList();

        _context.GameCompanies.RemoveRange(removed);

        var existing = game.GameCompanies
            .Except(removed)
            .Select(x => (x.CompanySource, x.CompanySourceId, x.Role))
            .ToHashSet();

        foreach (var reference in references.Where(x =>
                     !existing.Contains((x.Source, x.Id, x.Role))))
        {
            game.GameCompanies.Add(new GameCompany
            {
                GameId = game.Id,
                CompanySource = reference.Source,
                CompanySourceId = reference.Id,
                Role = reference.Role
            });
        }
    }

    private void SyncIgdbOverrideGenres(
        IgdbGameOverride gameOverride,
        IReadOnlyCollection<CatalogReferenceDTO> references)
    {
        var desired = references
            .Select(x => (x.Source, x.Id))
            .ToHashSet();

        var removed = gameOverride.Genres
            .Where(x => !desired.Contains((x.GenreSource, x.GenreSourceId)))
            .ToList();

        _context.IgdbGameGenreOverrides.RemoveRange(removed);

        var existing = gameOverride.Genres
            .Except(removed)
            .Select(x => (x.GenreSource, x.GenreSourceId))
            .ToHashSet();

        foreach (var reference in references.Where(x => !existing.Contains((x.Source, x.Id))))
        {
            gameOverride.Genres.Add(new IgdbGameGenreOverride
            {
                GenreSource = reference.Source,
                GenreSourceId = reference.Id
            });
        }

        gameOverride.ReplaceGenres = true;
    }

    private void SyncIgdbOverridePlatforms(
        IgdbGameOverride gameOverride,
        IReadOnlyCollection<CatalogReferenceDTO> references)
    {
        var desired = references
            .Select(x => (x.Source, x.Id))
            .ToHashSet();

        var removed = gameOverride.Platforms
            .Where(x => !desired.Contains((x.PlatformSource, x.PlatformSourceId)))
            .ToList();

        _context.IgdbGamePlatformOverrides.RemoveRange(removed);

        var existing = gameOverride.Platforms
            .Except(removed)
            .Select(x => (x.PlatformSource, x.PlatformSourceId))
            .ToHashSet();

        foreach (var reference in references.Where(x => !existing.Contains((x.Source, x.Id))))
        {
            gameOverride.Platforms.Add(new IgdbGamePlatformOverride
            {
                PlatformSource = reference.Source,
                PlatformSourceId = reference.Id
            });
        }

        gameOverride.ReplacePlatforms = true;
    }

    private void SyncIgdbOverrideCompanies(
        IgdbGameOverride gameOverride,
        IReadOnlyCollection<CompanyReferenceDTO> references)
    {
        var desired = references
            .Select(x => (x.Source, x.Id, x.Role))
            .ToHashSet();

        var removed = gameOverride.Companies
            .Where(x => !desired.Contains((x.CompanySource, x.CompanySourceId, x.Role)))
            .ToList();

        _context.IgdbGameCompanyOverrides.RemoveRange(removed);

        var existing = gameOverride.Companies
            .Except(removed)
            .Select(x => (x.CompanySource, x.CompanySourceId, x.Role))
            .ToHashSet();

        foreach (var reference in references.Where(x =>
                     !existing.Contains((x.Source, x.Id, x.Role))))
        {
            gameOverride.Companies.Add(new IgdbGameCompanyOverride
            {
                CompanySource = reference.Source,
                CompanySourceId = reference.Id,
                Role = reference.Role
            });
        }

        gameOverride.ReplaceCompanies = true;
    }

    private async Task UpdateGameListSnapshotsAsync(
        CatalogSource source,
        long gameId,
        string title,
        string? coverImageUrl,
        DateOnly? releaseDate,
        double? rating,
        CancellationToken cancellationToken)
    {
        var items = await _context.GameListItems
            .Where(x =>
                x.GameSource == source &&
                x.GameSourceId == gameId)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.GameTitle = title;
            item.CoverImageUrl = coverImageUrl;
            item.ReleaseDate = releaseDate;
            item.Rating = rating;
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
        {
            throw new ConflictException(
                "Kataloq məlumatı eyni vaxtda dəyişdirilib. Sorğunu yenidən göndərin.");
        }
    }

    private static GameListItemDTO ToListItem(GameDetailDTO game)
    {
        return new GameListItemDTO
        {
            Source = game.Source,
            Id = game.Id,
            Title = game.Title,
            CoverImageUrl = game.CoverImageUrl,
            ReleaseDate = game.ReleaseDate,
            AverageRating = game.AverageRating,
            IsOverridden = game.IsOverridden,
            GameType = game.GameType,
            Genres = game.Genres,
            Platforms = game.Platforms
        };
    }

    private static List<CatalogReferenceDTO> NormalizeCatalogReferences(
        IEnumerable<CatalogReferenceDTO>? references,
        string fieldName)
    {
        if (references is null)
            return [];

        var result = references.ToList();

        foreach (var reference in result)
        {
            if (reference is null)
                throw new BadRequestException($"{fieldName} istinadı boş ola bilməz.");

            reference.Id.EnsureValidCatalogReference(reference.Source, fieldName);
        }

        return result
            .DistinctBy(x => new { x.Source, x.Id })
            .ToList();
    }

    private static List<CompanyReferenceDTO> NormalizeCompanyReferences(
        IEnumerable<CompanyReferenceDTO>? references)
    {
        if (references is null)
        {
            return [];
        }

        var result = references.ToList();

        foreach (var reference in result)
        {
            if (reference is null)
            {
                throw new BadRequestException("Şirkət istinadı boş ola bilməz.");
            }

            reference.Id.EnsureValidCatalogReference(reference.Source, "Şirkət");

            if (!Enum.IsDefined(reference.Role))
            {
                throw new BadRequestException("Şirkət rolu düzgün deyil.");
            }
        }

        return result.DistinctBy(x => new { x.Source, x.Id, x.Role }).ToList();
    }

    private static void ValidateFilter(GameFilterDTO filter)
    {
        if (filter.Source.HasValue)
        {
            filter.Source.Value.EnsureValidCatalogSource();
        }

        if (filter.PageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (filter.PageSize is < 1 or > MaxPageSize)
        {
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
        }

        if ((long)(filter.PageNumber - 1) * filter.PageSize > int.MaxValue)
        {
            throw new BadRequestException("Səhifə nömrəsi və ölçüsünün kombinasiyası çox böyükdür.");
        }

        if (filter.SearchName?.Trim().Length > 200) throw new BadRequestException("Axtarış mətni maksimum 200 simvol ola bilər.");

        ValidateFilterReference(filter.CompanySource, filter.CompanyId, "Şirkət");
        ValidateFilterReference(filter.GenreSource, filter.GenreId, "Janr");
        ValidateFilterReference(filter.PlatformSource, filter.PlatformId, "Platforma");

        if (filter.ReleaseYear is < 1900 or > 2200)
            throw new BadRequestException("Çıxış ili düzgün deyil.");

        var allowedSorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "name_asc",
            "name_desc",
            "rating_asc",
            "rating_desc",
            "play_asc",
            "play_desc",
            "year_asc",
            "year_desc"
        };

        if (!string.IsNullOrWhiteSpace(filter.SortBy) &&
            !allowedSorts.Contains(filter.SortBy.Trim()))
        {
            throw new BadRequestException("SortBy dəyəri düzgün deyil.");
        }

        if (filter.Source == CatalogSource.Igdb &&
            (filter.CompanySource == CatalogSource.Local ||
             filter.GenreSource == CatalogSource.Local ||
             filter.PlatformSource == CatalogSource.Local))
        {
            throw new BadRequestException(
                "IGDB oyunlarını local kataloq istinadı ilə filterləmək mümkün deyil.");
        }

    }

    private static void ValidateFilterReference(
        CatalogSource? source,
        long? id,
        string entityName)
    {
        if (source.HasValue != id.HasValue)
        {
            throw new BadRequestException(
                $"{entityName} filterində mənbə və ID birlikdə göndərilməlidir.");
        }

        if (!source.HasValue)
            return;

        id!.Value.EnsureValidCatalogReference(source.Value, entityName);
    }

    private async Task<string> CreateUniqueSlugAsync(
    string title,
    int? ignoredGameId = null,
    CancellationToken cancellationToken = default)
    {
        var baseSlug = SlugHelper.Generate(title);
        var slug = baseSlug;
        var number = 2;

        while (await _context.Games
            .IgnoreQueryFilters()
            .AnyAsync(
                x => x.Slug == slug &&
                     (!ignoredGameId.HasValue ||
                      x.Id != ignoredGameId.Value),
                cancellationToken))
        {
            slug = $"{baseSlug}-{number}";
            number++;
        }

        return slug;
    }
}
