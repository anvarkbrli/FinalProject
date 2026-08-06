using System.Security.Claims;
using System.Text.Json;

using Ludora.Application.DTOs.Steam;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Ludora.API.Controllers.User
{
    [ApiController]
    [Route("api/settings/export/steam")]
    public sealed class SteamSettingsController : ControllerBase
    {
        private readonly ISteamService _steamService;
        private readonly SteamSettings _settings;
        private readonly ISteamSyncQueue _steamSyncQueue;

        public SteamSettingsController(
            ISteamService steamService,
            ISteamSyncQueue steamSyncQueue,
            IOptions<SteamSettings> options)
        {
            _steamService = steamService;
            _steamSyncQueue = steamSyncQueue;
            _settings = options.Value;
        }

        [Authorize]
        [HttpGet("connect")]
        public async Task<ActionResult<SteamConnectStartDTO>> StartConnection(
            CancellationToken cancellationToken)
        {
            var result = await _steamService.StartConnectionAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string? state,
            [FromQuery] string? responseMode,
            CancellationToken cancellationToken)
        {
            var parameters = Request.Query.ToDictionary(
                x => x.Key,
                x => x.Value.ToString(),
                StringComparer.Ordinal);

            var result = await _steamService.CompleteConnectionAsync(
                state ?? string.Empty,
                parameters,
                cancellationToken);

            if (result.Success && result.UserId.HasValue)
                await _steamSyncQueue.EnqueueAsync(result.UserId.Value, cancellationToken);

            if (string.Equals(responseMode, "json", StringComparison.OrdinalIgnoreCase))
                return Ok(result);

            return Content(BuildPopupResponse(result), "text/html; charset=utf-8");
        }

        [Authorize]
        [HttpGet("status")]
        public async Task<ActionResult<SteamConnectionStatusDTO>> GetStatus(
            CancellationToken cancellationToken)
        {
            var result = await _steamService.GetConnectionStatusAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> Disconnect(
            CancellationToken cancellationToken)
        {
            await _steamService.DisconnectAsync(
                GetCurrentUserId(),
                cancellationToken);

            return NoContent();
        }

        private string BuildPopupResponse(SteamConnectionResultDTO result)
        {
            var payload = JsonSerializer.Serialize(new
            {
                type = "wellplayd:steam-link-result",
                success = result.Success,
                steamId = result.SteamId,
                steamName = result.SteamName,
                avatarUrl = result.AvatarUrl,
                profileUrl = result.ProfileUrl,
                linkedAtUtc = result.LinkedAtUtc,
                error = result.Error
            });

            var redirectUrl = QueryHelpers.AddQueryString(
                _settings.FrontendSettingsUrl,
                new Dictionary<string, string?>
                {
                    ["steamSuccess"] = result.Success ? "true" : "false",
                    ["steamName"] = result.SteamName,
                    ["steamError"] = result.Error
                });

            var targetOriginJson = JsonSerializer.Serialize(_settings.FrontendOrigin);
            var redirectUrlJson = JsonSerializer.Serialize(redirectUrl);

            return $$"""
            <!doctype html>
            <html lang="az">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>Steam bağlantısı</title>
            </head>
            <body>
                <script>
                    (() => {
                        const payload = {{payload}};
                        const targetOrigin = {{targetOriginJson}};
                        const fallbackUrl = {{redirectUrlJson}};

                        if (window.opener && !window.opener.closed) {
                            window.opener.postMessage(payload, targetOrigin);
                            window.close();
                            return;
                        }

                        window.location.replace(fallbackUrl);
                    })();
                </script>
                <noscript>Steam bağlantısı tamamlandı. Ayarlar səhifəsinə geri qayıdın.</noscript>
            </body>
            </html>
            """;
        }

        private Guid GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(value, out var userId))
                throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");

            return userId;
        }
    }
}
