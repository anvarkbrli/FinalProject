using System.Text.Json;
using Ludora.API.Localization;
using Ludora.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Ludora.API.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _next = next;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client request-i ləğv edib; yeni response yazmağa çalışmırıq.
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(
                    exception,
                    "Response başladıqdan sonra idarə olunmamış xəta baş verdi.");
                throw;
            }

            var statusCode = GetStatusCode(exception);

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "İdarə olunmamış server xətası baş verdi.");
            }
            else
            {
                _logger.LogWarning(
                    exception,
                    "Request {StatusCode} statusu ilə dayandırıldı.",
                    statusCode);
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var localizedException = _localizer[exception.Message];
            var message = !localizedException.ResourceNotFound
                ? localizedException.Value
                : statusCode switch
                {
                    StatusCodes.Status400BadRequest => _localizer["BadRequest"],
                    StatusCodes.Status401Unauthorized => _localizer["Unauthorized"],
                    StatusCodes.Status403Forbidden => _localizer["Forbidden"],
                    StatusCodes.Status404NotFound => _localizer["NotFound"],
                    StatusCodes.Status409Conflict => _localizer["Conflict"],
                    StatusCodes.Status503ServiceUnavailable => _localizer["ExternalServiceUnavailable"],
                    _ => _localizer["UnexpectedServerError"]
                };

            var response = new
            {
                statusCode,
                message
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response),
                context.RequestAborted);
        }
    }

    private static int GetStatusCode(Exception exception)
    {
        return exception switch
        {
            BadRequestException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            DbUpdateException => StatusCodes.Status409Conflict,
            ExternalServiceException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}
