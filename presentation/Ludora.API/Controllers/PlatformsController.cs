using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;

using Microsoft.AspNetCore.Authorization;
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
        public async Task<ActionResult<IReadOnlyList<PlatformDetailDTO>>> GetAll([FromQuery] string? searchName,[FromQuery] CatalogSource? source = null, CancellationToken cancellationToken = default)
        {
            return Ok(await _platformService.GetAllAsync(searchName, source, cancellationToken));
        }

        [Authorize(Roles = RoleNames.AdminOrModerator)]
        [HttpGet("deleted")]
        public async Task<ActionResult<IReadOnlyList<PlatformDetailDTO>>> GetDeleted(
            CancellationToken cancellationToken)
        {
            return Ok(await _platformService.GetDeletedAsync(cancellationToken));
        }

        [HttpGet("{source}/{id:long:min(1)}")]
        public async Task<ActionResult<PlatformDetailDTO>> GetById(CatalogSource source,long id, CancellationToken cancellationToken)
        {
            return Ok(await _platformService.GetByIdAsync(source, id, cancellationToken));
        }

        [Authorize(Roles = RoleNames.AdminOrModerator)]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PlatformCreateDTO dto,CancellationToken cancellationToken)
        {
            var id = await _platformService.CreateAsync(dto, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { source = CatalogSource.Local, id },
                new { source = CatalogSource.Local, id });
        }

        [Authorize(Roles = RoleNames.AdminOrModerator)]
        [HttpPatch("{source}/{id:long:min(1)}")]
        public async Task<IActionResult> Update(CatalogSource source,long id,[FromBody] PlatformUpdateDTO dto, CancellationToken cancellationToken)
        {
            await _platformService.UpdateAsync(source, id, dto, cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = RoleNames.Admin)]
        [HttpDelete("{source}/{id:long:min(1)}")]
        public async Task<IActionResult> Delete(CatalogSource source,long id,CancellationToken cancellationToken)
        {
            await _platformService.DeleteAsync(source, id, cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = RoleNames.AdminOrModerator)]
        [HttpPatch("{source}/{id:long:min(1)}/restore")]
        public async Task<IActionResult> Restore(CatalogSource source,long id,CancellationToken cancellationToken)
        {
            await _platformService.RestoreAsync(source, id, cancellationToken);
            return NoContent();
        }
    }
}
