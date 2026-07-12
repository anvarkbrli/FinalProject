using Ludora.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.Interfaces
{
    public interface IGameListService
    {
        Task<List<GameListSummaryDTO>> GetUserListsAsync(Guid userId, Guid? currentUserId);
        Task<GameListDetailDTO> GetByIdAsync(int id, Guid? currentUserId);
        Task<int> CreateAsync(Guid userId, GameListCreateDTO dto);
        Task UpdateAsync(int id, Guid userId, GameListUpdateDTO dto);
        Task DeleteAsync(int id, Guid userId);
        Task AddGameAsync(int listId, int gameId, Guid userId);
        Task RemoveGameAsync(int listId, int gameId, Guid userId);
    }

}
