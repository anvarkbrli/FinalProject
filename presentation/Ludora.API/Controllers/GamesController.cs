using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Route("api/games")]
[ApiController]
public sealed class GamesController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly IFileStorageService _fileStorageService;

    public GamesController(
        IGameService gameService,
        IFileStorageService fileStorageService)
    {
        _gameService = gameService;
        _fileStorageService = fileStorageService;
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

    [Authorize(Roles = RoleNames.AdminOrModerator)]
    [HttpGet("deleted")]
    public async Task<ActionResult<IReadOnlyList<GameDetailDTO>>> GetDeleted(
        CancellationToken cancellationToken)
    {
        return Ok(await _gameService.GetDeletedAsync(cancellationToken));
    }

    [Authorize(Roles = RoleNames.AdminOrModerator)]
    [HttpPost("cover-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<object>> UploadCoverImage(
        IFormFile image,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length <= 0)
            return BadRequest(new { message = "Oyun şəkli seçilməyib." });

        await using var stream = image.OpenReadStream();
        var upload = new ProfileImageUploadDTO
        {
            Stream = stream,
            FileName = image.FileName,
            ContentType = image.ContentType,
            Length = image.Length
        };

        var url = await _fileStorageService.SaveGameCoverImageAsync(
            upload,
            cancellationToken);

        return Ok(new { url });
    }

    [Authorize(Roles = RoleNames.AdminOrModerator)]
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

    [Authorize(Roles = RoleNames.AdminOrModerator)]
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

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{source}/{id:long:min(1)}")]
    public async Task<IActionResult> Delete(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        await _gameService.DeleteAsync(source, id, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = RoleNames.AdminOrModerator)]
    [HttpPost("{source}/{id:long:min(1)}/relations")]
    public async Task<IActionResult> AddRelation(
        CatalogSource source,
        long id,
        [FromBody] GameRelationCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var relationId = await _gameService.AddRelationAsync(
            source,
            id,
            dto,
            cancellationToken);

        return Ok(new { id = relationId });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{source}/{id:long:min(1)}/relations/{relatedSource}/{relatedId:long:min(1)}")]
    public async Task<IActionResult> RemoveRelation(
        CatalogSource source,
        long id,
        CatalogSource relatedSource,
        long relatedId,
        [FromQuery] GameType relationType,
        CancellationToken cancellationToken)
    {
        await _gameService.RemoveRelationAsync(
            source,
            id,
            relatedSource,
            relatedId,
            relationType,
            cancellationToken);

        return NoContent();
    }

    [Authorize(Roles = RoleNames.AdminOrModerator)]
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
