using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ludora.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameListsController : ControllerBase
    {
        private readonly IGameListService _gameListService;

        public GameListsController(IGameListService gameListService)
        {
            _gameListService = gameListService;
        }

        [HttpGet("user/{userId:guid}")]
        public async Task<IActionResult> GetUserLists(Guid userId)
        {
            var result = await _gameListService.GetUserListsAsync(userId, GetOptionalCurrentUserId());

            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _gameListService.GetByIdAsync(id, GetOptionalCurrentUserId());

            return Ok(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GameListCreateDTO dto)
        {
            var id = await _gameListService.CreateAsync(GetCurrentUserId(), dto);

            return StatusCode(StatusCodes.Status201Created, new { id });
        }

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] GameListUpdateDTO dto)
        {
            await _gameListService.UpdateAsync(id, GetCurrentUserId(), dto);

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _gameListService.DeleteAsync(id, GetCurrentUserId());

            return NoContent();
        }

        [Authorize]
        [HttpPost("{listId:int}/games/{gameId:int}")]
        public async Task<IActionResult> AddGame(int listId, int gameId)
        {
            await _gameListService.AddGameAsync(listId, gameId, GetCurrentUserId());

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{listId:int}/games/{gameId:int}")]
        public async Task<IActionResult> RemoveGame(int listId, int gameId)
        {
            await _gameListService.RemoveGameAsync(listId, gameId, GetCurrentUserId());

            return NoContent();
        }

        private Guid GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userId, out var parsedUserId))
                throw new UnauthorizedAccessException("İstifadəçi sistemə daxil olmayıb.");

            return parsedUserId;
        }

        private Guid? GetOptionalCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : null;
        }
    }
}
