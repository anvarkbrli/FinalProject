using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserGamesController : ControllerBase
    {
        private readonly IUserGameService _userGameService;

        public UserGamesController(IUserGameService userGameService)
        {
            _userGameService = userGameService;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<UserGameDTO>>> GetMine(
            [FromQuery] UserGameStatus? status,
            [FromQuery] bool? isFavorite,
            [FromQuery] bool? isWishlisted,
            CancellationToken cancellationToken)
        {
            var result =
                await _userGameService.GetMineAsync(
                    GetCurrentUserId(),
                    status,
                    isFavorite,
                    isWishlisted,
                    cancellationToken);

            return Ok(result);
        }

        [HttpGet("{source}/{gameId:long}")]
        public async Task<ActionResult<UserGameDTO?>> GetState(CatalogSource source,long gameId,CancellationToken cancellationToken)
        {
            var result =
                await _userGameService.GetStateAsync(
                    GetCurrentUserId(),
                    source,
                    gameId,
                    cancellationToken);

            return Ok(result);
        }

        [HttpPut("{source}/{gameId:long}/status")]
        public async Task<ActionResult<UserGameDTO>>SetStatus(CatalogSource source,long gameId,
            [FromBody] SetUserGameStatusDTO dto,CancellationToken cancellationToken)
        {
            var result =
                await _userGameService.SetStatusAsync(
                    GetCurrentUserId(),
                    source,
                    gameId,
                    dto.Status,
                    cancellationToken);

            return Ok(result);
        }

        [HttpDelete("{source}/{gameId:long}/status")]
        public async Task<ActionResult<UserGameDTO?>> RemoveStatus(CatalogSource source, long gameId, CancellationToken cancellationToken)
        {
            var result =
                await _userGameService.RemoveStatusAsync(
                    GetCurrentUserId(),
                    source,
                    gameId,
                    cancellationToken);

            if (result is null)
            {
                return NoContent();
            }

            return Ok(result);
        }

        [HttpPut("{source}/{gameId:long}/favorite")]
        public async Task<ActionResult<UserGameDTO?>> SetFavorite(CatalogSource source, long gameId, 
            [FromBody] SetUserGameFavoriteDto dto, CancellationToken cancellationToken)
        {
            var result =
                await _userGameService.SetFavoriteAsync(
                    GetCurrentUserId(),
                    source,
                    gameId,
                    dto.IsFavorite,
                    cancellationToken);

            if (result is null)
            {
                return NoContent();
            }

            return Ok(result);
        }

        [HttpPut("{source}/{gameId:long}/wishlist")]
        public async Task<ActionResult<UserGameDTO?>> SetWishlist(CatalogSource source, long gameId,
                [FromBody] SetUserGameWishlistDTO dto,
                CancellationToken cancellationToken)
        {
            var result =
                await _userGameService.SetWishlistAsync(
                    GetCurrentUserId(),
                    source,
                    gameId,
                    dto.IsWishlisted,
                    cancellationToken);

            if (result is null)
            {
                return NoContent();
            }

            return Ok(result);
        }

        [HttpPut("{source}/{gameId:long}/rating")]
        public async Task<ActionResult<UserGameDTO?>> SetRating(
            CatalogSource source,
            long gameId,
            [FromBody] SetUserGameRatingDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _userGameService.SetRatingAsync(
                GetCurrentUserId(),
                source,
                gameId,
                dto.Rating,
                cancellationToken);

            if (result is null)
            {
                return NoContent();
            }

            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdValue,out var userId))
            {
                throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");
            }

            return userId;
        }
    }
}