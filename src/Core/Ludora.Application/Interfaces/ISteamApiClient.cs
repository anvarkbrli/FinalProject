using Ludora.Application.Models.Steam;

namespace Ludora.Application.Interfaces
{
    public interface ISteamApiClient
    {
        bool IsEnabled { get; }

        Task<SteamPlayerProfileModel?> GetPlayerSummaryAsync(
            string steamId,
            bool bypassCache = false,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SteamPlayerProfileModel>> GetPlayerSummariesAsync(
            IReadOnlyCollection<string> steamIds,
            CancellationToken cancellationToken = default);

        Task<SteamOwnedGamesResultModel> GetOwnedGamesAsync(
            string steamId,
            CancellationToken cancellationToken = default);

        Task<SteamFriendsResultModel> GetFriendsAsync(
            string steamId,
            CancellationToken cancellationToken = default);

        Task<SteamPlayerAchievementsResultModel> GetPlayerAchievementsAsync(
            string steamId,
            int appId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<string, SteamAchievementSchemaModel>> GetAchievementSchemaAsync(
            int appId,
            CancellationToken cancellationToken = default);
    }
}
