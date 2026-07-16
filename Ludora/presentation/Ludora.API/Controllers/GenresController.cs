using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Route("api/genres")]
[ApiController]
public sealed class GenresController : ControllerBase
{
    private readonly IGenreService _genreService;

    public GenresController(IGenreService genreService)
    {
        _genreService = genreService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GenreDetailDTO>>> GetAll(
        [FromQuery] string? searchName,
        [FromQuery] CatalogSource? source = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _genreService.GetAllAsync(searchName, source, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("deleted")]
    public async Task<ActionResult<IReadOnlyList<GenreDetailDTO>>> GetDeleted(
        CancellationToken cancellationToken)
    {
        return Ok(await _genreService.GetDeletedAsync(cancellationToken));
    }

    [HttpGet("{source}/{id:long:min(1)}")]
    public async Task<ActionResult<GenreDetailDTO>> GetById(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        return Ok(await _genreService.GetByIdAsync(source, id, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] GenreCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var id = await _genreService.CreateAsync(dto, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { source = CatalogSource.Local, id },
            new { source = CatalogSource.Local, id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{source}/{id:long:min(1)}")]
    public async Task<IActionResult> Update(
        CatalogSource source,
        long id,
        [FromBody] GenreUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        await _genreService.UpdateAsync(source, id, dto, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{source}/{id:long:min(1)}")]
    public async Task<IActionResult> Delete(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        await _genreService.DeleteAsync(source, id, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{source}/{id:long:min(1)}/restore")]
    public async Task<IActionResult> Restore(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        await _genreService.RestoreAsync(source, id, cancellationToken);
        return NoContent();
    }
}
