using Ludora.Application.DTOs.Steam;

namespace Ludora.Application.Interfaces;

public interface ISteamService
{
    Task<SteamConnectStartDTO> StartConnectionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SteamConnectionResultDTO> CompleteConnectionAsync(
        string state,
        IReadOnlyDictionary<string, string> callbackParameters,
        CancellationToken cancellationToken = default);

    Task<SteamConnectionStatusDTO> GetConnectionStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SteamSyncResultDTO> SyncOwnedGamesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SteamPlaytimeReconciliationDTO> GetPlaytimeReconciliationAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SteamApplyPlaytimeResultDTO> ApplyPlaytimeReconciliationAsync(
        Guid userId,
        SteamApplyPlaytimeReconciliationDTO dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SteamOwnedGameDTO>> GetOwnedGamesAsync(
        Guid userId,
        Guid? currentUserId,
        bool refresh,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SteamComingSoonGameDTO>> GetComingSoonAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SteamAchievementDTO>> GetRecentAchievementsAsync(
        Guid userId,
        int steamAppId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<SteamPresenceDTO> GetPresenceAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SteamFriendSyncDTO> SyncFriendsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
