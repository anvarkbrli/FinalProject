using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers.User;

[Route("api/game-lists")]
[ApiController]
public sealed class GameListsController : ControllerBase
{
    private readonly IGameListService _gameListService;

    public GameListsController(IGameListService gameListService)
    {
        _gameListService = gameListService;
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetUserLists(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await _gameListService.GetUserListsAsync(
            userId,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [HttpGet("{id:int:min(1)}")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await _gameListService.GetByIdAsync(
            id,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] GameListCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var id = await _gameListService.CreateAsync(
            GetCurrentUserId(),
            dto,
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [Authorize]
    [HttpPatch("{id:int:min(1)}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] GameListUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        await _gameListService.UpdateAsync(
            id,
            GetCurrentUserId(),
            dto,
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:int:min(1)}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        await _gameListService.DeleteAsync(id, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPatch("{id:int:min(1)}/restore")]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
    {
        await _gameListService.RestoreAsync(id, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{listId:int:min(1)}/games/{source}/{gameId:long:min(1)}")]
    public async Task<IActionResult> AddGame(
        int listId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken)
    {
        await _gameListService.AddGameAsync(
            listId,
            source,
            gameId,
            GetCurrentUserId(),
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{listId:int:min(1)}/games/{source}/{gameId:long:min(1)}")]
    public async Task<IActionResult> RemoveGame(
        int listId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken)
    {
        await _gameListService.RemoveGameAsync(
            listId,
            source,
            gameId,
            GetCurrentUserId(),
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPatch("{listId:int:min(1)}/games/{source}/{gameId:long:min(1)}/position")]
    public async Task<IActionResult> UpdateGamePosition(
        int listId,
        CatalogSource source,
        long gameId,
        [FromBody] GameListItemPositionUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        await _gameListService.UpdateGamePositionAsync(
            listId,
            source,
            gameId,
            dto.Position,
            GetCurrentUserId(),
            cancellationToken);

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
