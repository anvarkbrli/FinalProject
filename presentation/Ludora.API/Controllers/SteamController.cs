using System.Security.Claims;

using Ludora.Application.DTOs.Steam;
using Ludora.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers
{
    [ApiController]
    [Route("api/steam")]
    public sealed class SteamController : ControllerBase
    {
        private readonly ISteamService _steamService;
        private readonly IBadgeEvaluationQueue _badgeQueue;

        public SteamController(
            ISteamService steamService,
            IBadgeEvaluationQueue badgeQueue)
        {
            _steamService = steamService;
            _badgeQueue = badgeQueue;
        }

        [Authorize]
        [EnableRateLimiting("SteamSync")]
        [HttpPost("sync/owned-games")]
        public async Task<ActionResult<SteamSyncResultDTO>> SyncOwnedGames(
            CancellationToken cancellationToken)
        {
            var result = await _steamService.SyncOwnedGamesAsync(
                GetCurrentUserId(),
                cancellationToken);
            await _badgeQueue.EnqueueAsync(GetCurrentUserId(), cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("playtime-reconciliation")]
        public async Task<ActionResult<SteamPlaytimeReconciliationDTO>> GetPlaytimeReconciliation(
            CancellationToken cancellationToken)
        {
            var result = await _steamService.GetPlaytimeReconciliationAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpPost("playtime-reconciliation/apply")]
        public async Task<ActionResult<SteamApplyPlaytimeResultDTO>> ApplyPlaytimeReconciliation(
            [FromBody] SteamApplyPlaytimeReconciliationDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _steamService.ApplyPlaytimeReconciliationAsync(
                GetCurrentUserId(),
                dto,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("owned-games")]
        public async Task<ActionResult<IReadOnlyList<SteamOwnedGameDTO>>> GetMyOwnedGames(
            [FromQuery] bool refresh = false,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            var result = await _steamService.GetOwnedGamesAsync(
                userId,
                userId,
                refresh,
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("users/{userId:guid}/owned-games")]
        public async Task<ActionResult<IReadOnlyList<SteamOwnedGameDTO>>> GetUserOwnedGames(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var result = await _steamService.GetOwnedGamesAsync(
                userId,
                GetOptionalCurrentUserId(),
                refresh: false,
                cancellationToken: cancellationToken);

            return Ok(result);
        }

        [HttpGet("coming-soon")]
        public async Task<ActionResult<IReadOnlyList<SteamComingSoonGameDTO>>> GetComingSoon(
            [FromQuery] int limit = 12,
            CancellationToken cancellationToken = default)
        {
            var result = await _steamService.GetComingSoonAsync(limit, cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting("SteamSync")]
        [HttpGet("achievements/{steamAppId:int}/recent")]
        public async Task<ActionResult<IReadOnlyList<SteamAchievementDTO>>> GetRecentAchievements(
            int steamAppId,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _steamService.GetRecentAchievementsAsync(
                GetCurrentUserId(),
                steamAppId,
                limit,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting("SteamSync")]
        [HttpGet("presence")]
        public async Task<ActionResult<SteamPresenceDTO>> GetPresence(
            CancellationToken cancellationToken)
        {
            var result = await _steamService.GetPresenceAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting("SteamSync")]
        [HttpPost("friends/sync")]
        public async Task<ActionResult<SteamFriendSyncDTO>> SyncFriends(
            CancellationToken cancellationToken)
        {
            var result = await _steamService.SyncFriendsAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(value, out var userId))
                throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");

            return userId;
        }

        private Guid? GetOptionalCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }
}
