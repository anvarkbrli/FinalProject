using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers
{
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
        public async Task<ActionResult<List<GenreDetailDTO>>> GetAll(
            [FromQuery] string? searchName)
        {
            var genres = await _genreService.GetAllAsync(searchName);
            return Ok(genres);
        }

        [HttpGet("deleted")]
        public async Task<ActionResult<List<GenreDetailDTO>>> GetDeleted(
            [FromQuery] string? searchName)
        {
            var genres = await _genreService.GetDeletedAsync(searchName);
            return Ok(genres);
        }

        [HttpGet("{id:int:min(1)}")]
        public async Task<ActionResult<GenreDetailDTO>> GetById(int id)
        {
            var genre = await _genreService.GetByIdAsync(id);
            return Ok(genre);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GenreCreateDTO createDto)
        {
            var genreId = await _genreService.CreateAsync(createDto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = genreId },
                new { id = genreId });
        }

        [HttpPatch("{id:int:min(1)}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] GenreUpdateDTO updateDto)
        {
            await _genreService.UpdateAsync(id, updateDto);
            return NoContent();
        }

        [HttpDelete("{id:int:min(1)}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _genreService.DeleteAsync(id);
            return NoContent();
        }

        [HttpPatch("{id:int:min(1)}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            await _genreService.RestoreAsync(id);
            return NoContent();
        }
    }
}
