using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Route("api/forum/categories")]
[ApiController]
public sealed class ForumCategoriesController : ControllerBase
{
    private readonly IForumCategoryService _categoryService;

    public ForumCategoriesController(IForumCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ForumCategoryDTO>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.GetAllAsync(cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("deleted")]
    public async Task<ActionResult<IReadOnlyList<ForumCategoryDTO>>> GetDeleted(
        CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.GetDeletedAsync(cancellationToken));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ForumCategoryDTO>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.GetBySlugAsync(slug, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ForumCategoryDTO>> Create(
        [FromBody] ForumCategoryCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.CreateAsync(dto, cancellationToken);

        return CreatedAtAction(
            nameof(GetBySlug),
            new { slug = result.Slug },
            result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ForumCategoryDTO>> Update(
        int id,
        [FromBody] ForumCategoryUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.UpdateAsync(id, dto, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        await _categoryService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/restore")]
    public async Task<ActionResult<ForumCategoryDTO>> Restore(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.RestoreAsync(id, cancellationToken));
    }
}
