using Ludora.Application.Models.Steam;

namespace Ludora.Application.Interfaces
{
    public interface ISteamStoreClient
    {
        Task<IReadOnlyList<SteamComingSoonGameModel>> GetComingSoonAsync(
            int limit,
            CancellationToken cancellationToken = default);
    }
}
