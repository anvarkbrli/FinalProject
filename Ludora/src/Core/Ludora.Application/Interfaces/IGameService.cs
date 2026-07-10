using Ludora.Application.DTOs;
using Ludora.Application.DTOs.GameList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.Interfaces
{
    public interface IGameService
    {
        Task<GameDetailDTO> GetByIdAsync(int id);
        Task<IEnumerable<GameListItemDTO>> GetFilteredGamesAsync(GameFilterDTO filterDto);
        Task CreateAsync(GameCreateDTO createDto);
        Task UpdateAsync(int id, GameUpdateDTO updateDto);
        Task DeleteAsync(int id);

        // Dropdown-lar üçün yüngül axtarış/listləmə metodları
        Task<List<LookupDTO>> GetCompaniesLookupAsync();
        Task<List<LookupDTO>> GetGenresLookupAsync();
        Task<List<LookupDTO>> GetPlatformsLookupAsync();
    }
}
