using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Route("api/home")]
[ApiController]
public sealed class HomeController : ControllerBase
{
    private readonly IHomeDiscoveryService _homeDiscoveryService;

    public HomeController(IHomeDiscoveryService homeDiscoveryService)
    {
        _homeDiscoveryService = homeDiscoveryService;
    }

    [HttpGet("discovery")]
    public async Task<IActionResult> GetDiscovery(
        CancellationToken cancellationToken)
    {
        return Ok(await _homeDiscoveryService.GetAsync(cancellationToken));
    }
}
