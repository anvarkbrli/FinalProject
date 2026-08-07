using System.Security.Claims;

using Ludora.Application.DTOs.FpsEstimator;
using Ludora.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers
{
    [ApiController]
    [Route("api/fps-estimator")]
    [Authorize]
    public sealed class FpsEstimatorController : ControllerBase
    {
        private readonly IFpsEstimatorService _fpsEstimatorService;
        private readonly IUserService _userService;

        public FpsEstimatorController(
            IFpsEstimatorService fpsEstimatorService,
            IUserService userService)
        {
            _fpsEstimatorService = fpsEstimatorService;
            _userService = userService;
        }

        [EnableRateLimiting("FpsEstimator")]
        [HttpPost("estimate")]
        public async Task<ActionResult<FpsEstimateResponseDTO>> Estimate(
            [FromBody] FpsEstimateRequestDTO request,
            CancellationToken cancellationToken)
        {
            Response.Headers["X-WellPlayd-Fps-Version"] = "v7-final";

            return Ok(
                await _fpsEstimatorService.EstimateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken));
        }

        [HttpGet("hardware-profile")]
        public async Task<ActionResult<FpsHardwareProfileDTO>> GetHardwareProfile(
            CancellationToken cancellationToken)
        {
            return Ok(
                await _userService.GetFpsHardwareProfileAsync(
                    GetCurrentUserId(),
                    cancellationToken));
        }

        [HttpPut("hardware-profile")]
        public async Task<ActionResult<FpsHardwareProfileDTO>> UpdateHardwareProfile(
            [FromBody] UpdateFpsHardwareProfileDTO request,
            CancellationToken cancellationToken)
        {
            return Ok(
                await _userService.UpdateFpsHardwareProfileAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken));
        }

        private Guid GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException(
                    "İstifadəçi müəyyən edilmədi.");
            }

            return userId;
        }
    }
}
