using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Route("api/search")]
[ApiController]
public sealed class SearchController : ControllerBase
{
    private readonly IGlobalSearchService _searchService;

    public SearchController(IGlobalSearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<ActionResult<GlobalSearchResultDTO>> Search(
        [FromQuery] GlobalSearchFilterDTO filter,
        CancellationToken cancellationToken)
    {
        var result = await _searchService.SearchAsync(
            filter,
            GetOptionalCurrentUserId(),
            cancellationToken);

        return Ok(result);
    }

    private Guid? GetOptionalCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }
}
