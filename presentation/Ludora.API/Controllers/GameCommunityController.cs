using System.Security.Claims;

using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Game;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;

using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers
{
    [Route("api/games/{source}/{gameId:long:min(1)}/community")]
    [ApiController]
    public sealed class GameCommunityController : ControllerBase
    {
        private readonly IGameCommunityService _gameCommunityService;

        public GameCommunityController(
            IGameCommunityService gameCommunityService)
        {
            _gameCommunityService = gameCommunityService;
        }

        [HttpGet("categories/{category}/users")]
        public async Task<ActionResult<PagedResultDTO<GameCommunityUserDTO>>> GetCategoryUsers(
            CatalogSource source,
            long gameId,
            string category,
            [FromQuery] GameCommunityUsersFilterDTO filter,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<GameCommunityUserCategory>(
                    category,
                    ignoreCase: true,
                    out var parsedCategory))
            {
                throw new BadRequestException(
                    "İstifadəçi kateqoriyası düzgün deyil.");
            }

            var result = await _gameCommunityService.GetCategoryUsersAsync(
                source,
                gameId,
                parsedCategory,
                filter,
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("ratings/{rating:double}/users")]
        public async Task<ActionResult<PagedResultDTO<GameCommunityUserDTO>>> GetRatingUsers(
            CatalogSource source,
            long gameId,
            double rating,
            [FromQuery] GameCommunityUsersFilterDTO filter,
            CancellationToken cancellationToken)
        {
            var result = await _gameCommunityService.GetRatingUsersAsync(
                source,
                gameId,
                rating,
                filter,
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("wishlist-preview")]
        public async Task<ActionResult<IReadOnlyList<GameCommunityUserDTO>>> GetWishlistPreview(
            CatalogSource source,
            long gameId,
            [FromQuery] int limit = 3,
            CancellationToken cancellationToken = default)
        {
            var currentUserId = GetOptionalCurrentUserId();

            if (!currentUserId.HasValue)
                return Ok(Array.Empty<GameCommunityUserDTO>());

            var result = await _gameCommunityService.GetWishlistPreviewAsync(
                source,
                gameId,
                currentUserId.Value,
                limit,
                cancellationToken);

            return Ok(result);
        }

        private Guid? GetOptionalCurrentUserId()
        {
            var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(claimValue, out var userId)
                ? userId
                : null;
        }
    }
}
