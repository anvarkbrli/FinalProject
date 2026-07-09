using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers
{
    [Route("api/platforms")]
    [ApiController]
    public sealed class PlatformsController : ControllerBase
    {
        private readonly IPlatformService _platformService;

        public PlatformsController(IPlatformService platformService)
        {
            _platformService = platformService;
        }

        [HttpGet]
        public async Task<ActionResult<List<PlatformDetailDTO>>> GetAll(
            [FromQuery] string? searchName)
        {
            var platforms = await _platformService.GetAllAsync(searchName);
            return Ok(platforms);
        }

        [HttpGet("deleted")]
        public async Task<ActionResult<List<PlatformDetailDTO>>> GetDeleted(
            [FromQuery] string? searchName)
        {
            var platforms = await _platformService.GetDeletedAsync(searchName);
            return Ok(platforms);
        }

        [HttpGet("{id:int:min(1)}")]
        public async Task<ActionResult<PlatformDetailDTO>> GetById(int id)
        {
            var platform = await _platformService.GetByIdAsync(id);
            return Ok(platform);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PlatformCreateDTO createDto)
        {
            var platformId = await _platformService.CreateAsync(createDto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = platformId },
                new { id = platformId });
        }

        [HttpPatch("{id:int:min(1)}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] PlatformUpdateDTO updateDto)
        {
            await _platformService.UpdateAsync(id, updateDto);
            return NoContent();
        }

        [HttpDelete("{id:int:min(1)}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _platformService.DeleteAsync(id);
            return NoContent();
        }

        [HttpPatch("{id:int:min(1)}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            await _platformService.RestoreAsync(id);
            return NoContent();
        }
    }
}
