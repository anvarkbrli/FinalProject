using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Route("api/games")]
[ApiController]
public sealed class GamesController : ControllerBase
{
    private readonly IGameService _gameService;

    public GamesController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] GameFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _gameService.GetFilteredGamesAsync(filter, cancellationToken));
    }

    [HttpGet("{source}/{id:long:min(1)}")]
    public async Task<IActionResult> GetById(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        return Ok(await _gameService.GetByIdAsync(source, id, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] GameCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var id = await _gameService.CreateAsync(dto, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { source = CatalogSource.Local, id },
            new { source = CatalogSource.Local, id });
    }

    [HttpPatch("{source}/{id:long:min(1)}")]
    public async Task<IActionResult> Update(
        CatalogSource source,
        long id,
        [FromBody] GameUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        await _gameService.UpdateAsync(source, id, dto, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{source}/{id:long:min(1)}")]
    public async Task<IActionResult> Delete(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        await _gameService.DeleteAsync(source, id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{source}/{id:long:min(1)}/restore")]
    public async Task<IActionResult> Restore(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        await _gameService.RestoreAsync(source, id, cancellationToken);
        return NoContent();
    }
}
