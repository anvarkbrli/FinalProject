using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Ludora.Application.DTOs.Steam;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Steam;
using Ludora.Application.Settings;
using Ludora.Domain.Entities;
using Ludora.Domain.Entities.GameTracking;
using Ludora.Domain.Entities.Steam;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludora.Persistence.Services;

public sealed class SteamService : ISteamService
{
    private const int SqlServerSafeParameterBatchSize = 500;
    private const int MaxReviewPlaytimeMinutes = 600000;
    private const int AzerbaijanUtcOffsetHours = 4;
    private const string LegacySteamAccountId = "legacy";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SyncLocks = new();
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;
    private readonly ISteamApiClient _steamApiClient;
    private readonly ISteamOpenIdClient _steamOpenIdClient;
    private readonly ISteamStoreClient _steamStoreClient;
    private readonly SteamSettings _settings;
    private readonly ILogger<SteamService> _logger;

    public SteamService(
        AppDbContext context,
        IIgdbClient igdbClient,
        ISteamApiClient steamApiClient,
        ISteamOpenIdClient steamOpenIdClient,
        ISteamStoreClient steamStoreClient,
        IOptions<SteamSettings> options,
        ILogger<SteamService> logger)
    {
        _context = context;
        _igdbClient = igdbClient;
        _steamApiClient = steamApiClient;
        _steamOpenIdClient = steamOpenIdClient;
        _steamStoreClient = steamStoreClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<SteamConnectStartDTO> StartConnectionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var now = DateTime.UtcNow;
        await _context.SteamLinkChallenges
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = now.AddMinutes(_settings.LinkStateLifetimeMinutes);

        _context.SteamLinkChallenges.Add(new SteamLinkChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StateHash = HashState(state),
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new SteamConnectStartDTO
        {
            AuthorizationUrl = _steamOpenIdClient.BuildAuthenticationUrl(state),
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public async Task<SteamConnectionResultDTO> CompleteConnectionAsync(
        string state,
        IReadOnlyDictionary<string, string> callbackParameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureEnabled();

            if (string.IsNullOrWhiteSpace(state))
                return Failure("Steam bağlantı state-i tapılmadı.");

            var stateHash = HashState(state);
            var now = DateTime.UtcNow;
            var challenge = await _context.SteamLinkChallenges
                .AsNoTracking()
                .Where(x => x.StateHash == stateHash)
                .Select(x => new
                {
                    x.Id,
                    x.UserId,
                    x.ExpiresAtUtc,
                    x.ConsumedAtUtc
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Failure("Steam bağlantı sorğusu tapılmadı və ya artıq silinib.");

            if (challenge.ConsumedAtUtc.HasValue)
                return Failure("Steam bağlantı sorğusu artıq istifadə edilib.");

            if (challenge.ExpiresAtUtc <= now)
                return Failure("Steam bağlantı sorğusunun vaxtı bitib. Prosesi yenidən başladın.");

            var validation = await _steamOpenIdClient.ValidateCallbackAsync(
                callbackParameters,
                cancellationToken);

            if (!validation.IsValid || string.IsNullOrWhiteSpace(validation.SteamId))
                return Failure("Steam OpenID cavabı doğrulana bilmədi.");

            var alreadyLinkedUserId = await _context.Users
                .AsNoTracking()
                .Where(x => x.SteamId == validation.SteamId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (alreadyLinkedUserId.HasValue && alreadyLinkedUserId.Value != challenge.UserId)
                return Failure("Bu Steam hesabı artıq başqa WellPlayd hesabına bağlıdır.");

            var profile = await _steamApiClient.GetPlayerSummaryAsync(
                validation.SteamId,
                bypassCache: true,
                cancellationToken: cancellationToken);

            if (profile is null)
                return Failure("Steam profil məlumatı alınmadı.");

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            var conflictingUserExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(
                    x => x.SteamId == profile.SteamId && x.Id != challenge.UserId,
                    cancellationToken);

            if (conflictingUserExists)
                return Failure("Bu Steam hesabı artıq başqa WellPlayd hesabına bağlıdır.");

            var claimedRows = await _context.SteamLinkChallenges
                .Where(x => x.Id == challenge.Id &&
                            x.ConsumedAtUtc == null &&
                            x.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.ConsumedAtUtc, now),
                    cancellationToken);

            if (claimedRows != 1)
                return Failure("Steam bağlantı sorğusu artıq istifadə edilib.");

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == challenge.UserId, cancellationToken);

            if (user is null)
                return Failure("WellPlayd istifadəçisi tapılmadı.");

            user.SteamId = profile.SteamId;
            user.SteamDisplayName = profile.PersonaName;
            user.SteamAvatarUrl = profile.AvatarFullUrl ?? profile.AvatarMediumUrl ?? profile.AvatarUrl;
            user.SteamProfileUrl = profile.ProfileUrl;
            user.SteamLinkedAtUtc = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SteamConnectionResultDTO
            {
                UserId = user.Id,
                Success = true,
                SteamId = user.SteamId,
                SteamName = user.SteamDisplayName,
                AvatarUrl = user.SteamAvatarUrl,
                ProfileUrl = user.SteamProfileUrl,
                LinkedAtUtc = user.SteamLinkedAtUtc
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is BadRequestException or
            ConflictException or
            ExternalServiceException or
            DbUpdateException or
            HttpRequestException)
        {
            _logger.LogWarning(exception, "Steam hesabının bağlanması tamamlanmadı.");
            return Failure(GetConnectionFailureMessage(exception));
        }
    }

    public async Task<SteamConnectionStatusDTO> GetConnectionStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.SteamId,
                x.SteamDisplayName,
                x.SteamAvatarUrl,
                x.SteamProfileUrl,
                x.SteamLinkedAtUtc,
                x.SteamLastSyncedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        return new SteamConnectionStatusDTO
        {
            IsConnected = !string.IsNullOrWhiteSpace(user.SteamId),
            SteamId = user.SteamId,
            SteamName = user.SteamDisplayName,
            AvatarUrl = user.SteamAvatarUrl,
            ProfileUrl = user.SteamProfileUrl,
            LinkedAtUtc = user.SteamLinkedAtUtc,
            LastSyncedAtUtc = user.SteamLastSyncedAtUtc
        };
    }

    public async Task DisconnectAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var syncLock = SyncLocks.GetOrAdd(
            userId,
            _ => new SemaphoreSlim(1, 1));
        await syncLock.WaitAsync(cancellationToken);

        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new NotFoundException("İstifadəçi tapılmadı.");

            await _context.SteamLinkChallenges
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.SteamFriendSnapshots
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.SteamOwnedGames
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Import baselines are deliberately retained. They make reconnecting
            // the same Steam account idempotent and prevent full playtime from
            // being added to Ludora a second time.
            user.SteamId = null;
            user.SteamDisplayName = null;
            user.SteamAvatarUrl = null;
            user.SteamProfileUrl = null;
            user.SteamLinkedAtUtc = null;
            user.SteamLastSyncedAtUtc = null;

            await _context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            syncLock.Release();
        }
    }

    public async Task<SteamSyncResultDTO> SyncOwnedGamesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var syncLock = SyncLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            return await SyncOwnedGamesCoreAsync(userId, cancellationToken);
        }
        finally
        {
            syncLock.Release();
        }
    }

    private async Task<SteamSyncResultDTO> SyncOwnedGamesCoreAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var user = await GetLinkedUserAsync(userId, tracking: true, cancellationToken: cancellationToken);
        var steamAccountId = user.SteamId!;
        var remoteResult = await _steamApiClient.GetOwnedGamesAsync(
            steamAccountId,
            cancellationToken);

        if (!remoteResult.IsVisible)
        {
            throw new BadRequestException(
                "Steam oyun detalları gizlidir. Steam profilində Game details görünürlüğünü Public edin.");
        }

        var remoteGames = remoteResult.Games
            .GroupBy(x => x.AppId)
            .Select(x => x.OrderByDescending(g => g.PlaytimeForeverMinutes).First())
            .ToList();

        var catalogMatches = await MatchCatalogGamesAsync(
            remoteGames,
            cancellationToken);
        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var existingOwnedGames = await _context.SteamOwnedGames
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var ownedMap = existingOwnedGames.ToDictionary(x => x.SteamAppId);

        var existingUserGames = await _context.UserGames
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var userGameMap = existingUserGames
            .ToDictionary(x => (x.Source, x.GameId));

        var existingReviews = await _context.Reviews
            .IgnoreQueryFilters()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var reviewMap = existingReviews
            .ToDictionary(x => (x.Source, x.GameId));

        var existingLogs = await _context.UserGamePlayLogs
            .Where(x =>
                x.UserId == userId &&
                (x.SteamAccountId == steamAccountId ||
                 x.SteamAccountId == LegacySteamAccountId))
            .ToListAsync(cancellationToken);
        var logMap = existingLogs
            .GroupBy(x => x.SteamAppId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x =>
                        x.SteamAccountId == steamAccountId)
                    .ThenByDescending(x => x.LastSyncedAtUtc)
                    .First());

        var createdUserGameCount = 0;
        var updatedUserGameCount = 0;
        var createdAutoLogCount = 0;
        var updatedAutoLogCount = 0;
        var createdPlayLogCount = 0;
        var updatedPlayLogCount = 0;
        var updatedReviewPlaytimeCount = 0;
        var remoteAppIds = remoteGames.Select(x => x.AppId).ToHashSet();

        foreach (var remoteGame in remoteGames)
        {
            catalogMatches.TryGetValue(remoteGame.AppId, out var match);
            var iconUrl = BuildSteamIconUrl(remoteGame.AppId, remoteGame.IconHash);
            var lastPlayedAtUtc = FromUnixSeconds(remoteGame.LastPlayedUnix);
            var steamPlayedOn = GetSteamPlayedOn(lastPlayedAtUtc, now);
            var steamTotalMinutes = Math.Max(
                0,
                remoteGame.PlaytimeForeverMinutes);

            var ownedGameWasExisting = ownedMap.TryGetValue(
                remoteGame.AppId,
                out var existingOwnedGame);
            SteamOwnedGame ownedGame;
            if (ownedGameWasExisting && existingOwnedGame is not null)
            {
                ownedGame = existingOwnedGame;
            }
            else
            {
                ownedGameWasExisting = false;
                ownedGame = new SteamOwnedGame
                {
                    UserId = userId,
                    SteamAppId = remoteGame.AppId
                };
                _context.SteamOwnedGames.Add(ownedGame);
                ownedMap[remoteGame.AppId] = ownedGame;
            }

            ownedGame.LocalGameId =
                match?.Source == CatalogSource.Local &&
                match.GameId <= int.MaxValue
                    ? (int)match.GameId
                    : null;
            ownedGame.MatchedGameSource = match?.Source;
            ownedGame.MatchedGameId = match?.GameId;
            ownedGame.MatchedGameName = match?.Name;
            ownedGame.MatchedCoverImageUrl = match?.CoverImageUrl;
            ownedGame.MatchedReleaseDate = match?.ReleaseDate;
            ownedGame.MatchMethod =
                match?.MatchMethod ?? SteamGameMatchMethod.None;
            ownedGame.MatchConfidence = match is null ? 0m : 1m;
            ownedGame.IsSteamFallback = false;
            ownedGame.Name = remoteGame.Name;
            ownedGame.PlaytimeMinutes = steamTotalMinutes;
            ownedGame.PlaytimeWindowsMinutes = remoteGame.PlaytimeWindowsMinutes;
            ownedGame.PlaytimeMacMinutes = remoteGame.PlaytimeMacMinutes;
            ownedGame.PlaytimeLinuxMinutes = remoteGame.PlaytimeLinuxMinutes;
            ownedGame.PlaytimeTwoWeeksMinutes = remoteGame.PlaytimeTwoWeeksMinutes;
            ownedGame.LastPlayedAtUtc = lastPlayedAtUtc;
            ownedGame.IconUrl = iconUrl;
            ownedGame.SyncedAtUtc = now;

            if (match is null)
                continue;

            var gameKey = (match.Source, match.GameId);
            reviewMap.TryGetValue(gameKey, out var review);
            var hasVisibleReview = review is not null && !review.IsDeleted;

            if (!userGameMap.TryGetValue(gameKey, out var userGame))
            {
                userGame = new UserGame
                {
                    UserId = userId,
                    Source = match.Source,
                    GameId = match.GameId,
                    Status = UserGameStatus.Played,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.UserGames.Add(userGame);
                userGameMap[gameKey] = userGame;
                createdUserGameCount++;
            }
            else if (!hasVisibleReview &&
                     userGame.Status != UserGameStatus.Played)
            {
                userGame.Status = UserGameStatus.Played;
                userGame.UpdatedAt = now;
                updatedUserGameCount++;
            }

            logMap.TryGetValue(remoteGame.AppId, out var playLog);
            if (playLog?.SteamAccountId == LegacySteamAccountId)
                playLog.SteamAccountId = steamAccountId;

            var previousSteamTotal = playLog?.PlaytimeMinutes ?? 0;
            var observedSteamDelta = Math.Max(
                0,
                steamTotalMinutes - previousSteamTotal);
            var requiresHistoricalRepair =
                ownedGameWasExisting &&
                hasVisibleReview &&
                review is not null &&
                !review.IsSteamAutoLogged &&
                !ownedGame.ResolutionConfirmedAtUtc.HasValue &&
                steamTotalMinutes > 0;
            var steamMinutesToApply = requiresHistoricalRepair
                ? steamTotalMinutes
                : observedSteamDelta;

            if (hasVisibleReview && review is not null)
            {
                var hasSteamManagedPlaytime =
                    review.IsSteamAutoLogged &&
                    !review.IsSteamPlaytimeOverridden;

                if (hasSteamManagedPlaytime)
                {
                    var normalizedSteamMinutes =
                        NormalizeSteamReviewPlaytime(steamTotalMinutes);

                    if (review.PlaytimeMinutes != normalizedSteamMinutes)
                    {
                        review.PlaytimeMinutes = normalizedSteamMinutes;
                        review.UpdatedAt = now;
                        updatedReviewPlaytimeCount++;
                        updatedAutoLogCount++;
                    }

                    ownedGame.ManualPlaytimeSnapshotMinutes = null;
                    ownedGame.PlaytimeResolution =
                        SteamPlaytimeResolution.SteamOnly;
                    ownedGame.ResolvedPlaytimeMinutes = steamTotalMinutes;
                    ownedGame.ResolutionConfirmedAtUtc = now;
                }
                else if (steamMinutesToApply > 0)
                {
                    var currentMinutes = Math.Max(
                        0,
                        review.PlaytimeMinutes ?? 0);
                    var combinedMinutes = AddPlaytimeSafely(
                        currentMinutes,
                        steamMinutesToApply);

                    if (combinedMinutes != currentMinutes)
                    {
                        review.PlaytimeMinutes =
                            NormalizeSteamReviewPlaytime(combinedMinutes);
                        review.UpdatedAt = now;
                        updatedReviewPlaytimeCount++;
                        updatedAutoLogCount++;
                    }

                    if (requiresHistoricalRepair)
                    {
                        ownedGame.ManualPlaytimeSnapshotMinutes = currentMinutes;
                        ownedGame.ManualPlatformSourceSnapshot =
                            review.PlatformSource;
                        ownedGame.ManualPlatformIdSnapshot =
                            review.PlatformId;
                        ownedGame.ManualPlayedOnSnapshot = review.PlayedOn;
                    }
                    else if (!ownedGame.ManualPlaytimeSnapshotMinutes.HasValue)
                    {
                        ownedGame.ManualPlaytimeSnapshotMinutes = Math.Max(
                            0,
                            currentMinutes - previousSteamTotal);
                        ownedGame.ManualPlatformSourceSnapshot =
                            review.PlatformSource;
                        ownedGame.ManualPlatformIdSnapshot =
                            review.PlatformId;
                        ownedGame.ManualPlayedOnSnapshot = review.PlayedOn;
                    }

                    ownedGame.PlaytimeResolution =
                        SteamPlaytimeResolution.SeparateFromSteam;
                    ownedGame.ResolvedPlaytimeMinutes = combinedMinutes;
                    ownedGame.ResolutionConfirmedAtUtc ??= now;
                }
                else if (!ownedGameWasExisting && playLog is not null)
                {
                    // The current Steam account was synced before disconnecting.
                    // Rebuild only the transient reconciliation snapshot; the
                    // review already contains the imported Steam contribution.
                    ownedGame.ManualPlaytimeSnapshotMinutes = Math.Max(
                        0,
                        (review.PlaytimeMinutes ?? 0) -
                        playLog.PlaytimeMinutes);
                    ownedGame.ManualPlatformSourceSnapshot =
                        review.PlatformSource;
                    ownedGame.ManualPlatformIdSnapshot =
                        review.PlatformId;
                    ownedGame.ManualPlayedOnSnapshot = review.PlayedOn;
                    ownedGame.PlaytimeResolution =
                        SteamPlaytimeResolution.SeparateFromSteam;
                    ownedGame.ResolvedPlaytimeMinutes =
                        review.PlaytimeMinutes;
                    ownedGame.ResolutionConfirmedAtUtc = now;
                }
            }
            else
            {
                if (review is null)
                {
                    review = new Review
                    {
                        UserId = userId,
                        Source = match.Source,
                        GameId = match.GameId
                    };
                    _context.Reviews.Add(review);
                    reviewMap[gameKey] = review;
                }

                review.Content = string.Empty;
                review.ContainsSpoilers = false;
                review.PlayedOn = steamPlayedOn;
                review.PlatformSource = CatalogSource.Igdb;
                review.PlatformId = 6;
                review.PlaytimeMinutes =
                    NormalizeSteamReviewPlaytime(steamTotalMinutes);
                review.IsSteamAutoLogged = true;
                review.IsSteamPlaytimeOverridden = false;
                review.IsSteamPlayedOnOverridden = false;
                review.CreatedAt = now;
                review.UpdatedAt = null;
                review.IsDeleted = false;
                review.DeletedAtUtc = null;

                ownedGame.PlaytimeResolution = SteamPlaytimeResolution.SteamOnly;
                ownedGame.ResolvedPlaytimeMinutes = steamTotalMinutes;
                ownedGame.ResolutionConfirmedAtUtc = now;
                createdAutoLogCount++;
            }

            if (playLog is null)
            {
                playLog = new UserGamePlayLog
                {
                    UserId = userId,
                    SteamAccountId = steamAccountId,
                    GameSource = match.Source,
                    GameId = match.GameId,
                    SteamAppId = remoteGame.AppId,
                    InitialImportedPlaytimeMinutes = steamTotalMinutes,
                    PlaytimeMinutes = steamTotalMinutes,
                    LastPlayedAtUtc = lastPlayedAtUtc,
                    FirstSyncedAtUtc = now,
                    LastSyncedAtUtc = now
                };
                _context.UserGamePlayLogs.Add(playLog);
                logMap[remoteGame.AppId] = playLog;
                createdPlayLogCount++;
            }
            else
            {
                playLog.GameSource = match.Source;
                playLog.GameId = match.GameId;
                playLog.PlaytimeMinutes = steamTotalMinutes;
                playLog.LastPlayedAtUtc = lastPlayedAtUtc;
                playLog.LastSyncedAtUtc = now;
                updatedPlayLogCount++;
            }

            var deltaToRecord = requiresHistoricalRepair
                ? steamTotalMinutes
                : observedSteamDelta;
            if (deltaToRecord > 0)
            {
                _context.SteamPlaytimeDeltas.Add(new SteamPlaytimeDelta
                {
                    UserGamePlayLog = playLog,
                    DeltaMinutes = deltaToRecord,
                    TotalPlaytimeMinutes = steamTotalMinutes,
                    OccurredOn = steamPlayedOn,
                    SyncedAtUtc = now
                });
            }
        }

        var staleOwnedGames = existingOwnedGames
            .Where(x => !remoteAppIds.Contains(x.SteamAppId))
            .ToList();

        if (staleOwnedGames.Count > 0)
            _context.SteamOwnedGames.RemoveRange(staleOwnedGames);

        user.SteamLastSyncedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SteamSyncResultDTO
        {
            SteamOwnedGameCount = remoteGames.Count,
            MatchedGameCount = catalogMatches.Count,
            UnmatchedGameCount = remoteGames.Count - catalogMatches.Count,
            CreatedUserGameCount = createdUserGameCount,
            UpdatedUserGameCount = updatedUserGameCount,
            CreatedAutoLogCount = createdAutoLogCount,
            UpdatedAutoLogCount = updatedAutoLogCount,
            UpdatedReviewPlaytimeCount = updatedReviewPlaytimeCount,
            CreatedPlayLogCount = createdPlayLogCount,
            UpdatedPlayLogCount = updatedPlayLogCount,
            SyncedAtUtc = now
        };
    }

    public async Task<SteamPlaytimeReconciliationDTO> GetPlaytimeReconciliationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _ = await GetLinkedUserAsync(
            userId,
            tracking: false,
            cancellationToken: cancellationToken);

        var ownedGames = await _context.SteamOwnedGames
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.PlaytimeMinutes > 0)
            .OrderByDescending(x => x.PlaytimeMinutes)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var reviews = await _context.Reviews
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var reviewMap = reviews.ToDictionary(x => (x.Source, x.GameId));
        var localPlatformNames = await LoadLocalPlatformNamesAsync(
            reviews,
            cancellationToken);
        var items = new List<SteamPlaytimeReconciliationItemDTO>();

        foreach (var ownedGame in ownedGames)
        {
            if (!TryGetMatchedReference(
                    ownedGame,
                    out var source,
                    out var gameId))
            {
                continue;
            }

            reviewMap.TryGetValue((source, gameId), out var review);
            var manualMinutes = GetManualPlaytimeMinutes(ownedGame, review);
            var platformReference = GetManualPlatformReference(
                ownedGame,
                review);
            var platformName = ResolvePlatformName(
                platformReference.Source,
                platformReference.Id,
                localPlatformNames);
            var isConfirmed = IsConfirmedResolution(
                ownedGame.PlaytimeResolution);
            var needsDecision = !isConfirmed &&
                                manualMinutes > 0 &&
                                (!platformReference.Source.HasValue ||
                                 IsPcPlatform(
                                     platformReference.Source.Value,
                                     platformReference.Id!.Value,
                                     platformName));
            var suggestedResolution = needsDecision
                ? SteamPlaytimeResolution.ConservativeMaximum
                : manualMinutes <= 0
                    ? SteamPlaytimeResolution.SteamOnly
                    : SteamPlaytimeResolution.SeparatePlatformAutomatic;
            var effectiveResolution = isConfirmed
                ? ownedGame.PlaytimeResolution
                : suggestedResolution;

            items.Add(new SteamPlaytimeReconciliationItemDTO
            {
                SteamAppId = ownedGame.SteamAppId,
                SteamName = ownedGame.Name,
                SteamIconUrl = ownedGame.IconUrl,
                SteamPlaytimeMinutes = ownedGame.PlaytimeMinutes,
                MatchedGameSource = source,
                MatchedGameId = gameId,
                MatchedGameName = ownedGame.MatchedGameName ??
                                  ownedGame.Name,
                MatchedCoverImageUrl = ownedGame.MatchedCoverImageUrl,
                MatchMethod = ownedGame.MatchMethod,
                ManualPlaytimeMinutes = manualMinutes,
                ManualPlatformName = platformName,
                CurrentResolution = needsDecision &&
                                    ownedGame.PlaytimeResolution ==
                                    SteamPlaytimeResolution.Unresolved
                    ? SteamPlaytimeResolution.PendingUserDecision
                    : ownedGame.PlaytimeResolution,
                SuggestedResolution = suggestedResolution,
                NeedsUserDecision = needsDecision,
                PreviewResolvedPlaytimeMinutes = CalculateResolvedPlaytime(
                    manualMinutes,
                    ownedGame.PlaytimeMinutes,
                    effectiveResolution)
            });
        }

        return new SteamPlaytimeReconciliationDTO
        {
            TotalPlayedGames = ownedGames.Count,
            PendingCount = items.Count(x => x.NeedsUserDecision),
            ReadyToApplyCount = items.Count(x =>
                !x.NeedsUserDecision &&
                !IsConfirmedResolution(x.CurrentResolution)),
            AlreadyConfirmedCount = items.Count(x =>
                IsConfirmedResolution(x.CurrentResolution)),
            Items = items
        };
    }

    public async Task<SteamApplyPlaytimeResultDTO>
        ApplyPlaytimeReconciliationAsync(
            Guid userId,
            SteamApplyPlaytimeReconciliationDTO dto,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!IsUserSelectableResolution(dto.DefaultPendingResolution))
        {
            throw new BadRequestException(
                "Standart Steam saat seçimi düzgün deyil.");
        }

        if (dto.Decisions.Any(x =>
                x.SteamAppId < 1 ||
                !IsUserSelectableResolution(x.Resolution)) ||
            dto.Decisions
                .GroupBy(x => x.SteamAppId)
                .Any(group => group.Count() > 1))
        {
            throw new BadRequestException(
                "Steam saat seçimlərindən biri düzgün deyil.");
        }

        _ = await GetLinkedUserAsync(
            userId,
            tracking: false,
            cancellationToken: cancellationToken);

        var ownedGames = await _context.SteamOwnedGames
            .Where(x => x.UserId == userId && x.PlaytimeMinutes > 0)
            .OrderBy(x => x.SteamAppId)
            .ToListAsync(cancellationToken);
        var reviews = await _context.Reviews
            .IgnoreQueryFilters()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var reviewMap = reviews.ToDictionary(x => (x.Source, x.GameId));
        var localPlatformNames = await LoadLocalPlatformNamesAsync(
            reviews,
            cancellationToken);
        var decisionMap = dto.Decisions.ToDictionary(
            x => x.SteamAppId,
            x => x.Resolution);
        var matchedAppIds = ownedGames
            .Where(x => TryGetMatchedReference(x, out _, out _))
            .Select(x => x.SteamAppId)
            .ToHashSet();
        var unknownDecision = decisionMap.Keys
            .FirstOrDefault(x => !matchedAppIds.Contains(x));

        if (unknownDecision > 0)
        {
            throw new BadRequestException(
                $"Steam App ID {unknownDecision} uyğunlaşdırılmış oyun deyil.");
        }

        var now = DateTime.UtcNow;
        var appliedGameCount = 0;
        var createdAutoLogCount = 0;
        var updatedAutoLogCount = 0;
        var assignedPcPlatformCount = 0;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        foreach (var ownedGame in ownedGames)
        {
            if (!TryGetMatchedReference(
                    ownedGame,
                    out var source,
                    out var gameId))
            {
                continue;
            }

            reviewMap.TryGetValue((source, gameId), out var review);
            var manualMinutes = GetManualPlaytimeMinutes(ownedGame, review);
            var platformReference = GetManualPlatformReference(
                ownedGame,
                review);
            var platformName = ResolvePlatformName(
                platformReference.Source,
                platformReference.Id,
                localPlatformNames);
            var needsDecision = manualMinutes > 0 &&
                                (!platformReference.Source.HasValue ||
                                 IsPcPlatform(
                                     platformReference.Source.Value,
                                     platformReference.Id!.Value,
                                     platformName));
            var automaticResolution = manualMinutes <= 0
                ? SteamPlaytimeResolution.SteamOnly
                : SteamPlaytimeResolution.SeparatePlatformAutomatic;
            var resolution = needsDecision
                ? decisionMap.GetValueOrDefault(
                    ownedGame.SteamAppId,
                    dto.DefaultPendingResolution)
                : automaticResolution;

            if (!IsApplicableResolution(resolution))
            {
                throw new BadRequestException(
                    $"Steam App ID {ownedGame.SteamAppId} üçün seçim düzgün deyil.");
            }

            if (!ownedGame.ManualPlaytimeSnapshotMinutes.HasValue)
            {
                ownedGame.ManualPlaytimeSnapshotMinutes = manualMinutes;
                ownedGame.ManualPlatformSourceSnapshot =
                    platformReference.Source;
                ownedGame.ManualPlatformIdSnapshot = platformReference.Id;
                ownedGame.ManualPlayedOnSnapshot = review?.PlayedOn;
            }

            var resolvedMinutes = CalculateResolvedPlaytime(
                manualMinutes,
                ownedGame.PlaytimeMinutes,
                resolution);
            var playedOn = GetSteamPlayedOn(
                ownedGame.LastPlayedAtUtc,
                ownedGame.SyncedAtUtc);

            if (review is null)
            {
                review = new Review
                {
                    UserId = userId,
                    Source = source,
                    GameId = gameId,
                    Content = string.Empty,
                    ContainsSpoilers = false,
                    PlayedOn = playedOn,
                    PlatformSource = CatalogSource.Igdb,
                    PlatformId = 6,
                    PlaytimeMinutes = NormalizeSteamReviewPlaytime(
                        resolvedMinutes),
                    IsSteamAutoLogged = true,
                    IsSteamPlaytimeOverridden = false,
                    IsSteamPlayedOnOverridden = false,
                    CreatedAt = now
                };
                _context.Reviews.Add(review);
                reviewMap[(source, gameId)] = review;
                createdAutoLogCount++;
                assignedPcPlatformCount++;
            }
            else
            {
                review.IsDeleted = false;
                review.DeletedAtUtc = null;
                review.PlaytimeMinutes = NormalizeSteamReviewPlaytime(
                    resolvedMinutes);

                if (!review.PlayedOn.HasValue)
                    review.PlayedOn = playedOn;

                if (!review.PlatformSource.HasValue ||
                    !review.PlatformId.HasValue)
                {
                    review.PlatformSource = CatalogSource.Igdb;
                    review.PlatformId = 6;
                    assignedPcPlatformCount++;
                }

                if (string.IsNullOrWhiteSpace(review.Content))
                    review.IsSteamAutoLogged = true;

                review.UpdatedAt = now;
                updatedAutoLogCount++;
            }

            ownedGame.PlaytimeResolution = resolution;
            ownedGame.ResolvedPlaytimeMinutes = resolvedMinutes;
            ownedGame.ResolutionConfirmedAtUtc = now;
            appliedGameCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SteamApplyPlaytimeResultDTO
        {
            AppliedGameCount = appliedGameCount,
            CreatedAutoLogCount = createdAutoLogCount,
            UpdatedAutoLogCount = updatedAutoLogCount,
            AssignedPcPlatformCount = assignedPcPlatformCount,
            AppliedAtUtc = now
        };
    }

    public async Task<IReadOnlyList<SteamOwnedGameDTO>> GetOwnedGamesAsync(
        Guid userId,
        Guid? currentUserId,
        bool refresh,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.IsPrivate,
                x.SteamId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        await EnsureCanViewSteamLibraryAsync(
            user.Id,
            user.IsPrivate,
            currentUserId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(user.SteamId))
            return [];

        var hasSnapshot = await _context.SteamOwnedGames
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId, cancellationToken);

        if ((refresh || !hasSnapshot) && currentUserId == userId)
            await SyncOwnedGamesAsync(userId, cancellationToken);

        return await _context.SteamOwnedGames
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.PlaytimeMinutes)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.SteamAppId)
            .Select(x => new SteamOwnedGameDTO
            {
                SteamAppId = x.SteamAppId,
                Name = x.Name,
                PlaytimeMinutes = x.PlaytimeMinutes,
                PlaytimeHours = Math.Round(x.PlaytimeMinutes / 60m, 2),
                PlaytimeWindowsMinutes = x.PlaytimeWindowsMinutes,
                PlaytimeMacMinutes = x.PlaytimeMacMinutes,
                PlaytimeLinuxMinutes = x.PlaytimeLinuxMinutes,
                PlaytimeTwoWeeksMinutes = x.PlaytimeTwoWeeksMinutes,
                LastPlayedAtUtc = x.LastPlayedAtUtc,
                SteamIconUrl = x.IconUrl,
                IsMatched = x.MatchedGameId.HasValue ||
                            x.LocalGameId.HasValue,
                MatchedGameSource = x.MatchedGameSource ??
                    (x.LocalGameId.HasValue
                        ? CatalogSource.Local
                        : null),
                MatchedGameId = x.MatchedGameId ??
                                x.LocalGameId,
                MatchedGameName = x.MatchedGameName ??
                                  (x.LocalGame == null
                                      ? null
                                      : x.LocalGame.Name),
                MatchedCoverImageUrl = x.MatchedCoverImageUrl ??
                    (x.LocalGame == null
                        ? null
                        : x.LocalGame.CoverImageUrl),
                MatchedReleaseDate = x.MatchedReleaseDate ??
                    (x.LocalGame == null
                        ? null
                        : x.LocalGame.ReleaseDate),
                MatchMethod = x.MatchMethod,
                MatchConfidence = x.MatchConfidence,
                IsSteamFallback = x.IsSteamFallback,
                PlaytimeResolution = x.PlaytimeResolution,
                ResolvedPlaytimeMinutes = x.ResolvedPlaytimeMinutes,
                NeedsResolution =
                    (x.PlaytimeResolution ==
                         SteamPlaytimeResolution.Unresolved ||
                     x.PlaytimeResolution ==
                         SteamPlaytimeResolution.PendingUserDecision) &&
                    x.PlaytimeMinutes > 0 &&
                    (x.MatchedGameId.HasValue ||
                     x.LocalGameId.HasValue),
                DetailUrl = x.MatchedGameId.HasValue
                    ? x.MatchedGameSource == CatalogSource.Igdb
                        ? "/games/Igdb/" + x.MatchedGameId
                        : "/games/Local/" + x.MatchedGameId
                    : x.LocalGameId.HasValue
                        ? "/games/Local/" + x.LocalGameId
                        : null,
                LocalGameId = x.LocalGameId,
                LocalGameName = x.LocalGame == null ? null : x.LocalGame.Name,
                LocalGameSlug = x.LocalGame == null ? null : x.LocalGame.Slug,
                LocalCoverImageUrl = x.LocalGame == null
                    ? null
                    : x.LocalGame.CoverImageUrl,
                LocalReleaseDate = x.LocalGame == null
                    ? null
                    : x.LocalGame.ReleaseDate,
                SyncedAtUtc = x.SyncedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SteamComingSoonGameDTO>> GetComingSoonAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 50)
            throw new BadRequestException("Limit 1-50 aralığında olmalıdır.");

        var games = await _steamStoreClient.GetComingSoonAsync(limit, cancellationToken);
        var appIds = games.Select(x => x.AppId).Distinct().ToList();
        var localGames = await LoadGamesBySteamAppIdsAsync(appIds, cancellationToken);
        var localMap = localGames
            .Where(x => x.SteamAppId.HasValue)
            .ToDictionary(x => x.SteamAppId!.Value);

        return games.Select(x =>
        {
            localMap.TryGetValue(x.AppId, out var localGame);
            var divisor = GetCurrencyMinorUnitDivisor(x.Currency);

            return new SteamComingSoonGameDTO
            {
                Rank = x.Rank,
                SteamAppId = x.AppId,
                Name = x.Name,
                HeaderImageUrl = x.HeaderImageUrl,
                SmallCapsuleImageUrl = x.SmallCapsuleImageUrl,
                PortraitImageUrl = x.PortraitImageUrl,
                ReleaseDateText = x.ReleaseDateText,
                ReleaseDate = x.ReleaseDate,
                StoreUrl = string.IsNullOrWhiteSpace(x.StoreUrl)
                    ? $"https://store.steampowered.com/app/{x.AppId}/"
                    : x.StoreUrl,
                RankingSource = "Steam Top Wishlists order",
                IsDiscounted = x.IsDiscounted,
                DiscountPercent = x.DiscountPercent,
                OriginalPrice = x.OriginalPriceMinor.HasValue
                    ? x.OriginalPriceMinor.Value / divisor
                    : null,
                FinalPrice = x.FinalPriceMinor.HasValue
                    ? x.FinalPriceMinor.Value / divisor
                    : null,
                Currency = x.Currency,
                IsMatched = localGame is not null,
                LocalGameId = localGame?.Id,
                LocalGameSlug = localGame?.Slug
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<SteamAchievementDTO>> GetRecentAchievementsAsync(
        Guid userId,
        int steamAppId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (steamAppId < 1)
            throw new BadRequestException("Steam App ID düzgün deyil.");

        if (limit is < 1 or > 50)
            throw new BadRequestException("Limit 1-50 aralığında olmalıdır.");

        var user = await GetLinkedUserAsync(userId, tracking: false, cancellationToken: cancellationToken);
        var achievementsTask = _steamApiClient.GetPlayerAchievementsAsync(
            user.SteamId!,
            steamAppId,
            cancellationToken);
        var schemaTask = _steamApiClient.GetAchievementSchemaAsync(
            steamAppId,
            cancellationToken);

        await Task.WhenAll(achievementsTask, schemaTask);

        var achievements = await achievementsTask;
        var schema = await schemaTask;

        if (!achievements.IsVisible)
        {
            throw new BadRequestException(
                "Steam nailiyyətləri görünmür. Profil və game details görünürlüğünü yoxlayın.");
        }

        var gameName = achievements.GameName ?? $"Steam App {steamAppId}";

        return achievements.Achievements
            .Where(x => x.Achieved && x.UnlockedAtUtc.HasValue)
            .OrderByDescending(x => x.UnlockedAtUtc)
            .Take(limit)
            .Select(x =>
            {
                schema.TryGetValue(x.ApiName, out var metadata);

                return new SteamAchievementDTO
                {
                    SteamAppId = steamAppId,
                    GameName = gameName,
                    ApiName = x.ApiName,
                    DisplayName = metadata?.DisplayName ?? x.ApiName,
                    Description = metadata?.Description,
                    IconUrl = metadata?.IconUrl,
                    UnlockedAtUtc = x.UnlockedAtUtc!.Value
                };
            })
            .ToList();
    }

    public async Task<SteamPresenceDTO> GetPresenceAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetLinkedUserAsync(userId, tracking: false, cancellationToken: cancellationToken);
        var profile = await _steamApiClient.GetPlayerSummaryAsync(
            user.SteamId!,
            bypassCache: true,
            cancellationToken: cancellationToken)
            ?? throw new ExternalServiceException("Steam profil statusu alınmadı.");

        return new SteamPresenceDTO
        {
            SteamId = profile.SteamId,
            SteamName = profile.PersonaName,
            IsOnline = profile.PersonaState != 0,
            IsPlaying = profile.GameAppId.HasValue || !string.IsNullOrWhiteSpace(profile.GameName),
            PersonaState = profile.PersonaState,
            PersonaStateName = GetPersonaStateName(profile.PersonaState),
            SteamAppId = profile.GameAppId,
            GameName = profile.GameName,
            LastLogoffAtUtc = FromUnixSeconds(profile.LastLogoffUnix)
        };
    }

    public async Task<SteamFriendSyncDTO> SyncFriendsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetLinkedUserAsync(userId, tracking: true, cancellationToken: cancellationToken);
        var result = await _steamApiClient.GetFriendsAsync(user.SteamId!, cancellationToken);
        var now = DateTime.UtcNow;

        if (!result.IsVisible)
        {
            return new SteamFriendSyncDTO
            {
                IsFriendListVisible = false,
                SyncedAtUtc = now
            };
        }

        var remoteFriends = result.Friends
            .GroupBy(x => x.SteamId, StringComparer.Ordinal)
            .Select(x => x.First())
            .ToList();
        var remoteIds = remoteFriends.Select(x => x.SteamId).ToHashSet(StringComparer.Ordinal);

        var snapshots = await _context.SteamFriendSnapshots
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var snapshotMap = snapshots.ToDictionary(x => x.FriendSteamId, StringComparer.Ordinal);

        foreach (var snapshot in snapshots)
            snapshot.IsActive = remoteIds.Contains(snapshot.FriendSteamId);

        foreach (var remoteFriend in remoteFriends)
        {
            if (!snapshotMap.TryGetValue(remoteFriend.SteamId, out var snapshot))
            {
                snapshot = new SteamFriendSnapshot
                {
                    UserId = userId,
                    FriendSteamId = remoteFriend.SteamId
                };
                _context.SteamFriendSnapshots.Add(snapshot);
                snapshotMap[remoteFriend.SteamId] = snapshot;
            }

            snapshot.FriendSinceUtc = remoteFriend.FriendSinceUtc;
            snapshot.LastSeenAtUtc = now;
            snapshot.IsActive = true;
        }

        var matchingUsers = await LoadUsersBySteamIdsAsync(remoteIds, userId, cancellationToken);
        var matchingUserIds = matchingUsers.Select(x => x.Id).ToList();
        var blockedUserIds = matchingUserIds.Count == 0
            ? new HashSet<Guid>()
            : (await _context.UserBlocks
                .AsNoTracking()
                .Where(x =>
                    (x.BlockerId == userId && matchingUserIds.Contains(x.BlockedUserId)) ||
                    (x.BlockedUserId == userId && matchingUserIds.Contains(x.BlockerId)))
                .Select(x => x.BlockerId == userId ? x.BlockedUserId : x.BlockerId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        matchingUsers = matchingUsers
            .Where(x => !blockedUserIds.Contains(x.Id))
            .ToList();

        var friendSinceMap = remoteFriends.ToDictionary(
            x => x.SteamId,
            x => x.FriendSinceUtc,
            StringComparer.Ordinal);

        user.SteamLastSyncedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        var suggestions = matchingUsers
            .Select(x => new SteamFriendSuggestionDTO
            {
                UserId = x.Id,
                UserName = x.UserName ?? string.Empty,
                ProfileImageUrl = x.ProfileImageUrl,
                SteamId = x.SteamId!,
                SteamName = x.SteamDisplayName,
                FriendSinceUtc = friendSinceMap.GetValueOrDefault(x.SteamId!)
            })
            .OrderBy(x => x.UserName)
            .ToList();

        return new SteamFriendSyncDTO
        {
            IsFriendListVisible = true,
            SteamFriendCount = remoteFriends.Count,
            WellPlaydMatchCount = suggestions.Count,
            SyncedAtUtc = now,
            Suggestions = suggestions
        };
    }

    private async Task<Dictionary<int, string>> LoadLocalPlatformNamesAsync(
        IReadOnlyCollection<Review> reviews,
        CancellationToken cancellationToken)
    {
        var platformIds = reviews
            .Where(x =>
                x.PlatformSource == CatalogSource.Local &&
                x.PlatformId is > 0 and <= int.MaxValue)
            .Select(x => (int)x.PlatformId!.Value)
            .Distinct()
            .ToArray();

        if (platformIds.Length == 0)
            return [];

        return await _context.Platforms
            .AsNoTracking()
            .Where(x => platformIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(
                x => x.Id,
                x => x.Name,
                cancellationToken);
    }

    private static bool TryGetMatchedReference(
        SteamOwnedGame ownedGame,
        out CatalogSource source,
        out long gameId)
    {
        if (ownedGame.MatchedGameSource.HasValue &&
            ownedGame.MatchedGameId.HasValue &&
            ownedGame.MatchedGameId.Value > 0)
        {
            source = ownedGame.MatchedGameSource.Value;
            gameId = ownedGame.MatchedGameId.Value;
            return true;
        }

        if (ownedGame.LocalGameId.HasValue)
        {
            source = CatalogSource.Local;
            gameId = ownedGame.LocalGameId.Value;
            return true;
        }

        source = default;
        gameId = 0;
        return false;
    }

    private static int GetManualPlaytimeMinutes(
        SteamOwnedGame ownedGame,
        Review? review)
    {
        if (ownedGame.ManualPlaytimeSnapshotMinutes.HasValue)
            return Math.Max(0, ownedGame.ManualPlaytimeSnapshotMinutes.Value);

        if (review is null || review.IsSteamAutoLogged)
            return 0;

        return Math.Max(0, review.PlaytimeMinutes ?? 0);
    }

    private static (
        CatalogSource? Source,
        long? Id) GetManualPlatformReference(
        SteamOwnedGame ownedGame,
        Review? review)
    {
        if (ownedGame.ManualPlatformSourceSnapshot.HasValue &&
            ownedGame.ManualPlatformIdSnapshot.HasValue)
        {
            return (
                ownedGame.ManualPlatformSourceSnapshot,
                ownedGame.ManualPlatformIdSnapshot);
        }

        return (review?.PlatformSource, review?.PlatformId);
    }

    private static string? ResolvePlatformName(
        CatalogSource? source,
        long? platformId,
        IReadOnlyDictionary<int, string> localPlatformNames)
    {
        if (!source.HasValue || !platformId.HasValue)
            return null;

        if (source == CatalogSource.Local &&
            platformId.Value <= int.MaxValue &&
            localPlatformNames.TryGetValue(
                (int)platformId.Value,
                out var localName))
        {
            return localName;
        }

        return source == CatalogSource.Igdb
            ? platformId.Value switch
            {
                3 => "Linux",
                6 => "PC (Microsoft Windows)",
                14 => "Mac",
                _ => $"IGDB platform #{platformId.Value}"
            }
            : $"Platform #{platformId.Value}";
    }

    private static bool IsPcPlatform(
        CatalogSource source,
        long platformId,
        string? platformName)
    {
        if (source == CatalogSource.Igdb &&
            platformId is 3 or 6 or 14)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(platformName))
            return false;

        return platformName.Contains(
                   "Windows",
                   StringComparison.OrdinalIgnoreCase) ||
               platformName.Contains(
                   "Linux",
                   StringComparison.OrdinalIgnoreCase) ||
               platformName.Contains(
                   "Mac",
                   StringComparison.OrdinalIgnoreCase) ||
               platformName.Contains(
                   "PC",
                   StringComparison.OrdinalIgnoreCase) ||
               platformName.Contains(
                   "Computer",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateResolvedPlaytime(
        int manualMinutes,
        int steamMinutes,
        SteamPlaytimeResolution resolution)
    {
        var manual = Math.Max(0, manualMinutes);
        var steam = Math.Max(0, steamMinutes);
        var result = resolution switch
        {
            SteamPlaytimeResolution.SeparateFromSteam or
            SteamPlaytimeResolution.SeparatePlatformAutomatic =>
                (long)manual + steam,
            SteamPlaytimeResolution.SteamIncludesManual or
            SteamPlaytimeResolution.ConservativeMaximum =>
                Math.Max(manual, steam),
            _ => steam
        };

        return (int)Math.Min(result, MaxReviewPlaytimeMinutes);
    }

    private static bool IsConfirmedResolution(
        SteamPlaytimeResolution resolution)
    {
        return resolution is not SteamPlaytimeResolution.Unresolved and
            not SteamPlaytimeResolution.PendingUserDecision;
    }

    private static bool IsUserSelectableResolution(
        SteamPlaytimeResolution resolution)
    {
        return resolution is
            SteamPlaytimeResolution.SteamIncludesManual or
            SteamPlaytimeResolution.SeparateFromSteam or
            SteamPlaytimeResolution.ConservativeMaximum;
    }

    private static bool IsApplicableResolution(
        SteamPlaytimeResolution resolution)
    {
        return IsUserSelectableResolution(resolution) ||
               resolution is
                   SteamPlaytimeResolution.SteamOnly or
                   SteamPlaytimeResolution.SeparatePlatformAutomatic;
    }

    private async Task<AppUser> GetLinkedUserAsync(
        Guid userId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<AppUser> query = _context.Users;

        if (!tracking)
            query = query.AsNoTracking();

        var user = await query.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        if (string.IsNullOrWhiteSpace(user.SteamId))
            throw new BadRequestException("Steam hesabı WellPlayd hesabına bağlı deyil.");

        return user;
    }

    private async Task<Dictionary<int, Game>> MatchLocalGamesAsync(
        IReadOnlyList<SteamOwnedGameModel> remoteGames,
        CancellationToken cancellationToken)
    {
        var appIds = remoteGames.Select(x => x.AppId).Distinct().ToList();
        var directMatches = await LoadGamesBySteamAppIdsAsync(appIds, cancellationToken);
        var result = directMatches
            .Where(x => x.SteamAppId.HasValue)
            .ToDictionary(x => x.SteamAppId!.Value);

        var unmatchedGames = remoteGames
            .Where(x => !result.ContainsKey(x.AppId))
            .ToList();

        if (unmatchedGames.Count == 0)
            return result;

        var names = unmatchedGames
            .Select(x => x.Name.Trim())
            .Where(static x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var nameCandidates = await LoadGamesByNamesAsync(names, cancellationToken);
        var candidateMap = nameCandidates
            .Where(x => !x.SteamAppId.HasValue)
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
        var claimedLocalIds = directMatches.Select(x => x.Id).ToHashSet();

        foreach (var remoteGame in unmatchedGames)
        {
            if (!candidateMap.TryGetValue(remoteGame.Name.Trim(), out var localGame) ||
                claimedLocalIds.Contains(localGame.Id))
            {
                continue;
            }

            localGame.SteamAppId = remoteGame.AppId;
            result[remoteGame.AppId] = localGame;
            claimedLocalIds.Add(localGame.Id);
        }

        return result;
    }

    private async Task<Dictionary<int, CatalogGameMatch>> MatchCatalogGamesAsync(
        IReadOnlyList<SteamOwnedGameModel> remoteGames,
        CancellationToken cancellationToken)
    {
        var localMatches = await MatchLocalGamesAsync(
            remoteGames,
            cancellationToken);
        var result = localMatches.ToDictionary(
            pair => pair.Key,
            pair => new CatalogGameMatch(
                CatalogSource.Local,
                pair.Value.Id,
                pair.Value.Name,
                pair.Value.CoverImageUrl,
                pair.Value.ReleaseDate,
                SteamGameMatchMethod.SteamAppId));

        var unmatchedAppIds = remoteGames
            .Where(x => !result.ContainsKey(x.AppId))
            .Select(x => x.AppId)
            .Distinct()
            .ToArray();

        if (unmatchedAppIds.Length == 0 || !_igdbClient.IsEnabled)
            return result;

        try
        {
            var mappings = await _igdbClient.GetSteamGameMappingsAsync(
                unmatchedAppIds,
                cancellationToken);
            if (mappings.Count == 0)
                return result;

            var igdbGames = await _igdbClient.GetGamesByIdsAsync(
                mappings.Select(x => x.IgdbGameId).Distinct().ToArray(),
                cancellationToken);
            var igdbGameMap = igdbGames.ToDictionary(x => x.Id);

            foreach (var mapping in mappings)
            {
                if (result.ContainsKey(mapping.SteamAppId) ||
                    !igdbGameMap.TryGetValue(mapping.IgdbGameId, out var game))
                {
                    continue;
                }

                result[mapping.SteamAppId] = new CatalogGameMatch(
                    CatalogSource.Igdb,
                    game.Id,
                    game.Name,
                    game.CoverImageUrl,
                    game.ReleaseDate,
                    SteamGameMatchMethod.IgdbExternalId);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "IGDB Steam oyun xəritələməsi uğursuz oldu; lokal uyğunluqlarla davam edilir.");
        }

        return result;
    }

    private async Task<List<Game>> LoadGamesBySteamAppIdsAsync(
        IReadOnlyCollection<int> appIds,
        CancellationToken cancellationToken)
    {
        var result = new List<Game>();

        foreach (var batch in appIds.Distinct().Chunk(SqlServerSafeParameterBatchSize))
        {
            result.AddRange(await _context.Games
                .Where(x => x.SteamAppId.HasValue && batch.Contains(x.SteamAppId.Value))
                .ToListAsync(cancellationToken));
        }

        return result
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
    }

    private async Task<List<Game>> LoadGamesByNamesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken)
    {
        var result = new List<Game>();

        foreach (var batch in names.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(SqlServerSafeParameterBatchSize))
        {
            result.AddRange(await _context.Games
                .Where(x => !x.SteamAppId.HasValue && batch.Contains(x.Name))
                .ToListAsync(cancellationToken));
        }

        return result
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
    }

    private async Task<List<AppUser>> LoadUsersBySteamIdsAsync(
        IReadOnlyCollection<string> steamIds,
        Guid excludedUserId,
        CancellationToken cancellationToken)
    {
        var result = new List<AppUser>();
        var now = DateTime.UtcNow;

        foreach (var batch in steamIds.Chunk(SqlServerSafeParameterBatchSize))
        {
            result.AddRange(await _context.Users
                .AsNoTracking()
                .Where(x => x.Id != excludedUserId &&
                            x.SteamId != null &&
                            !x.IsPermanentlyBanned &&
                            (!x.BannedUntilUtc.HasValue || x.BannedUntilUtc <= now) &&
                            batch.Contains(x.SteamId))
                .ToListAsync(cancellationToken));
        }

        return result
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
    }

    private async Task EnsureCanViewSteamLibraryAsync(
        Guid userId,
        bool isPrivate,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == userId)
            return;

        if (currentUserId.HasValue)
        {
            var hasBlock = await _context.UserBlocks
                .AsNoTracking()
                .AnyAsync(x =>
                        (x.BlockerId == currentUserId.Value && x.BlockedUserId == userId) ||
                        (x.BlockerId == userId && x.BlockedUserId == currentUserId.Value),
                    cancellationToken);

            if (hasBlock)
                throw new ForbiddenException("Bu istifadəçinin Steam kitabxanasına baxmaq mümkün deyil.");
        }

        if (!isPrivate)
            return;

        var isFollowing = currentUserId.HasValue &&
                          await _context.UserFollows
                              .AsNoTracking()
                              .AnyAsync(x =>
                                      x.FollowerId == currentUserId.Value &&
                                      x.FollowingId == userId &&
                                      x.Status == FollowStatus.Accepted,
                                  cancellationToken);

        if (!isFollowing)
            throw new ForbiddenException("Bu profil gizlidir.");
    }

    private void EnsureEnabled()
    {
        if (!_settings.Enabled || !_steamApiClient.IsEnabled)
        {
            throw new ExternalServiceException(
                "Steam inteqrasiyası deaktivdir. Steam:Enabled və Steam:ApiKey konfiqurasiyasını yoxlayın.");
        }
    }

    private static string GetConnectionFailureMessage(Exception exception)
    {
        return exception switch
        {
            DbUpdateException => "Bu Steam hesabı artıq başqa WellPlayd hesabına bağlıdır.",
            HttpRequestException => "Steam xidmətinə qoşulmaq mümkün olmadı. Yenidən yoxlayın.",
            _ => exception.Message
        };
    }

    private static SteamConnectionResultDTO Failure(string error)
    {
        return new SteamConnectionResultDTO
        {
            Success = false,
            Error = error
        };
    }

    private static string HashState(string state)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(state)));
    }

    private static string? BuildSteamIconUrl(int appId, string? iconHash)
    {
        return string.IsNullOrWhiteSpace(iconHash)
            ? null
            : $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{iconHash}.jpg";
    }

    private static DateTime? FromUnixSeconds(long? unixSeconds)
    {
        if (!unixSeconds.HasValue || unixSeconds.Value <= 0)
            return null;

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static int? NormalizeSteamReviewPlaytime(int playtimeMinutes)
    {
        if (playtimeMinutes <= 0)
            return null;

        return Math.Min(playtimeMinutes, MaxReviewPlaytimeMinutes);
    }

    private static int AddPlaytimeSafely(
        int currentMinutes,
        int additionalMinutes)
    {
        var result = (long)Math.Max(0, currentMinutes) +
                     Math.Max(0, additionalMinutes);
        return (int)Math.Min(result, MaxReviewPlaytimeMinutes);
    }

    private static DateOnly GetSteamPlayedOn(
        DateTime? lastPlayedAtUtc,
        DateTime syncedAtUtc)
    {
        var playedAtUtc = lastPlayedAtUtc ?? syncedAtUtc;

        return DateOnly.FromDateTime(
            playedAtUtc.AddHours(AzerbaijanUtcOffsetHours));
    }

    private static string GetPersonaStateName(int state)
    {
        return state switch
        {
            0 => "Offline",
            1 => "Online",
            2 => "Busy",
            3 => "Away",
            4 => "Snooze",
            5 => "LookingToTrade",
            6 => "LookingToPlay",
            _ => "Unknown"
        };
    }

    private static decimal GetCurrencyMinorUnitDivisor(string? currency)
    {
        return currency?.ToUpperInvariant() switch
        {
            "JPY" or "KRW" => 1m,
            _ => 100m
        };
    }

    private sealed record CatalogGameMatch(
        CatalogSource Source,
        long GameId,
        string Name,
        string? CoverImageUrl,
        DateOnly? ReleaseDate,
        SteamGameMatchMethod MatchMethod);
}
