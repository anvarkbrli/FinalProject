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

        if (filter.Source == CatalogSource.Local)
        {
            var localQuery = BuildLocalQuery(filter);
            var localTotalCount = await localQuery.CountAsync(cancellationToken);

            var localGames = await ApplyLocalSort(localQuery, filter.SortBy)
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

            var countTask = _igdbClient.GetGamesCountAsync(
                filter,
                hiddenIgdbGameIds,
                cancellationToken);

            var gamesTask = _igdbClient.GetGamesAsync(
                filter,
                hiddenIgdbGameIds,
                cancellationToken);

            await Task.WhenAll(countTask, gamesTask);

            var mappedIgdbGames = await MapIgdbGamesAsync(
                gamesTask.Result,
                cancellationToken);

            var items = SortGames(mappedIgdbGames, filter.SortBy)
                .Select(ToListItem)
                .ToList();

            return CreatePagedResult(
                items,
                pageNumber,
                pageSize,
                countTask.Result);
        }

        var requestedEnd = checked((long)pageNumber * pageSize);

        if (requestedEnd > int.MaxValue)
        {
            throw new BadRequestException(
                "Sorğu edilən səhifə aralığı çox böyükdür.");
        }

        var fetchCount = (int)requestedEnd;
        var details = new List<GameDetailDTO>();

        var combinedLocalQuery = BuildLocalQuery(filter);
        var localCount = await combinedLocalQuery.CountAsync(cancellationToken);

        var localCombinedGames = await ApplyLocalSort(
                combinedLocalQuery,
                filter.SortBy)
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

                var countTask = _igdbClient.GetGamesCountAsync(
                    igdbFilter,
                    hiddenIgdbGameIds,
                    cancellationToken);

                var gamesTask = GetIgdbGamesUpToAsync(
                    igdbFilter,
                    fetchCount,
                    hiddenIgdbGameIds,
                    cancellationToken);

                await Task.WhenAll(countTask, gamesTask);

                igdbCount = countTask.Result;

                details.AddRange(
                    await MapIgdbGamesAsync(
                        gamesTask.Result,
                        cancellationToken));
            }
            catch (ExternalServiceException)
            {
                // IGDB müvəqqəti əlçatmazdırsa local nəticələr yenə göstərilir.
            }
        }

        var itemsForPage = SortGames(details, filter.SortBy)
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
        var relatedIds = game.DlcIds
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

        if (relatedIds.Length == 0)
        {
            return new RelatedGamesDTO();
        }

        IReadOnlyList<IgdbGameModel> igdbGames;

        try
        {
            igdbGames = await _igdbClient.GetGamesByIdsAsync(
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

        List<GameListItemDTO> Resolve(
            IEnumerable<long> ids)
        {
            return ids
                .Distinct()
                .Where(gameMap.ContainsKey)
                .Select(id => gameMap[id])
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

        return query;
    }

    private static IQueryable<Game> ApplyLocalSort(
        IQueryable<Game> query,
        string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "name_asc" => query.OrderBy(x => x.Name),
            "name_desc" => query.OrderByDescending(x => x.Name),
            "rating_asc" => query.OrderBy(x => x.AverageRating),
            "rating_desc" => query.OrderByDescending(x => x.AverageRating),
            "play_asc" => query.OrderBy(x => x.PlayCount),
            "play_desc" => query.OrderByDescending(x => x.PlayCount),
            "year_asc" => query.OrderBy(x => x.ReleaseDate),
            "year_desc" => query.OrderByDescending(x => x.ReleaseDate),
            _ => query
                .OrderByDescending(x => x.ReleaseDate)
                .ThenBy(x => x.Name)
        };
    }

    private static IEnumerable<GameDetailDTO> SortGames(
        IEnumerable<GameDetailDTO> games,
        string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "name_asc" => games.OrderBy(x => x.Title),
            "name_desc" => games.OrderByDescending(x => x.Title),
            "rating_asc" => games.OrderBy(x => x.AverageRating ?? double.MaxValue),
            "rating_desc" => games.OrderByDescending(x => x.AverageRating ?? double.MinValue),
            "play_asc" => games.OrderBy(x => x.PlayCount),
            "play_desc" => games.OrderByDescending(x => x.PlayCount),
            "year_asc" => games.OrderBy(x => x.ReleaseDate ?? DateOnly.MaxValue),
            "year_desc" => games.OrderByDescending(x => x.ReleaseDate ?? DateOnly.MinValue),
            _ => games
                .OrderByDescending(x => x.ReleaseDate ?? DateOnly.MinValue)
                .ThenBy(x => x.Title)
        };
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
            .DistinctBy(reference => new { reference.Source, reference.Id })
            .ToList();

        if (normalizedReferences.Count == 0)
        {
            return [];
        }

        var localIds = normalizedReferences
            .Where(reference => reference.Source == CatalogSource.Local)
            .Select(reference => reference.Id)
            .ToArray();

        var igdbIds = normalizedReferences
            .Where(reference => reference.Source == CatalogSource.Igdb)
            .Select(reference => reference.Id)
            .ToArray();

        var rows = await _context.UserGames
            .AsNoTracking()
            .Where(userGame =>
                (userGame.Source == CatalogSource.Local && localIds.Contains(userGame.GameId)) ||
                (userGame.Source == CatalogSource.Igdb && igdbIds.Contains(userGame.GameId)))
            .GroupBy(userGame => new
            {
                userGame.Source,
                userGame.GameId
            })
            .Select(group => new
            {
                group.Key.Source,
                group.Key.GameId,
                AverageRating = group.Average(userGame => userGame.Rating),
                RatingCount = group.Count(userGame => userGame.Rating.HasValue),
                PlayingCount = group.Count(userGame =>
                    userGame.Status == UserGameStatus.Playing),
                PlanToPlayCount = group.Count(userGame =>
                    userGame.Status == UserGameStatus.PlanToPlay),
                PlayedCount = group.Count(userGame =>
                    userGame.Status == UserGameStatus.Played),
                DroppedCount = group.Count(userGame =>
                    userGame.Status == UserGameStatus.Dropped),
                WishlistCount = group.Count(userGame => userGame.IsWishlisted),
                FavoriteCount = group.Count(userGame => userGame.IsFavorite)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => (row.Source, row.GameId),
            row => new GameCommunityStatsDTO
            {
                AverageRating = row.AverageRating,
                RatingCount = row.RatingCount,
                PlayingCount = row.PlayingCount,
                PlanToPlayCount = row.PlanToPlayCount,
                PlayedCount = row.PlayedCount,
                DroppedCount = row.DroppedCount,
                WishlistCount = row.WishlistCount,
                FavoriteCount = row.FavoriteCount
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

        if (filter.Source == CatalogSource.Igdb &&
            filter.SortBy?.StartsWith("play_", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new BadRequestException(
                "IGDB oyunlarında oynanma sayına görə sıralama dəstəklənmir.");
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
