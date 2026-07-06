using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GamesController : ControllerBase
    {
        private readonly IGameService _gameService;

        public GamesController(IGameService gameService)
        {
            _gameService = gameService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] GameFilterDTO filterDto)
        {
            var games = await _gameService.GetFilteredGamesAsync(filterDto);
            return Ok(games);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var game = await _gameService.GetByIdAsync(id);
            return Ok(game);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] GameCreateDTO createDto)
        {
            await _gameService.CreateAsync(createDto);
            return StatusCode(201);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] GameUpdateDTO updateDto)
        {
            await _gameService.UpdateAsync(id, updateDto);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _gameService.DeleteAsync(id);
            return NoContent();
        }

        // --- Dropdown/Lookup Endpoints ---

        [HttpGet("lookup/companies")]
        public async Task<IActionResult> GetCompaniesLookup()
        {
            return Ok(await _gameService.GetCompaniesLookupAsync());
        }

        [HttpGet("lookup/genres")]
        public async Task<IActionResult> GetGenresLookup()
        {
            return Ok(await _gameService.GetGenresLookupAsync());
        }

        [HttpGet("lookup/platforms")]
        public async Task<IActionResult> GetPlatformsLookup()
        {
            return Ok(await _gameService.GetPlatformsLookupAsync());
        }
    }
}
