using System.Globalization;
using System.Security.Claims;

using Ludora.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers
{
    [Route("api/home")]
    [ApiController]
    public sealed class HomeController : ControllerBase
    {
        private readonly IHomeDiscoveryService _homeDiscoveryService;
        private readonly IHomeSocialService _homeSocialService;
        private readonly IHomeInterestRecommendationService
            _homeInterestRecommendationService;

        public HomeController(
            IHomeDiscoveryService homeDiscoveryService,
            IHomeSocialService homeSocialService,
            IHomeInterestRecommendationService homeInterestRecommendationService)
        {
            _homeDiscoveryService = homeDiscoveryService;
            _homeSocialService = homeSocialService;
            _homeInterestRecommendationService = homeInterestRecommendationService;
        }

        [HttpGet("discovery")]
        public async Task<IActionResult> GetDiscovery(
            CancellationToken cancellationToken)
        {
            return Ok(await _homeDiscoveryService.GetAsync(cancellationToken));
        }

        [Authorize]
        [HttpGet("social")]
        public async Task<IActionResult> GetSocialDashboard(
            CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
                return Unauthorized();

            return Ok(await _homeSocialService.GetAsync(userId, cancellationToken));
        }

        [Authorize]
        [HttpGet("interest-recommendations")]
        public async Task<IActionResult> GetInterestRecommendations(
            [FromQuery] string? language,
            CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
                return Unauthorized();

            var requestedLanguage = string.Equals(
                language,
                "en",
                StringComparison.OrdinalIgnoreCase)
                ? "en"
                : string.Equals(
                    language,
                    "az",
                    StringComparison.OrdinalIgnoreCase)
                    ? "az"
                    : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            return Ok(await _homeInterestRecommendationService.GetAsync(
                userId,
                requestedLanguage,
                cancellationToken));
        }
    }
}
