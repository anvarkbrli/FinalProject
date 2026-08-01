using System.Globalization;
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

            var hasRefreshSessionSchemaMismatch =
                IsRefreshSessionSchemaMismatch(exception);
            var statusCode = hasRefreshSessionSchemaMismatch
                ? StatusCodes.Status503ServiceUnavailable
                : GetStatusCode(exception);

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
            context.Response.ContentType = "application/json; charset=utf-8";

            var localizedException = _localizer[exception.Message];
            var isSafeApplicationException = exception is
                BadRequestException or
                UnauthorizedException or
                ForbiddenException or
                NotFoundException or
                ConflictException or
                ExternalServiceException;

            string message;
            if (hasRefreshSessionSchemaMismatch)
            {
                message = Localize(
                    "Errors.RefreshSessionSchemaMissing",
                    "Hesab sessiyası yaradıla bilmədi, çünki məlumat bazası yenilənməyib. Database migration-larını tətbiq edib yenidən cəhd edin.",
                    "The account session could not be created because the database is not up to date. Apply the database migrations and try again.");
            }
            else if (!localizedException.ResourceNotFound)
            {
                message = localizedException.Value;
            }
            else if (isSafeApplicationException &&
                     !string.IsNullOrWhiteSpace(exception.Message))
            {
                message = exception.Message;
            }
            else
            {
                message = exception switch
                {
                    DbUpdateException => Localize(
                        "Errors.DatabaseConflict",
                        "Məlumat bazasında eyni və ya ziddiyyətli məlumat mövcuddur.",
                        "The database contains duplicate or conflicting data."),
                    _ => statusCode switch
                    {
                        StatusCodes.Status400BadRequest => Localize(
                            "Errors.BadRequest",
                            "Sorğu məlumatları düzgün deyil.",
                            "The request data is invalid."),
                        StatusCodes.Status401Unauthorized => Localize(
                            "Errors.Unauthorized",
                            "Bu əməliyyat üçün sistemə daxil olmalısınız.",
                            "You must sign in to perform this action."),
                        StatusCodes.Status403Forbidden => Localize(
                            "Errors.Forbidden",
                            "Bu əməliyyat üçün icazəniz yoxdur.",
                            "You do not have permission to perform this action."),
                        StatusCodes.Status404NotFound => Localize(
                            "Errors.NotFound",
                            "Tələb olunan məlumat tapılmadı.",
                            "The requested resource was not found."),
                        StatusCodes.Status409Conflict => Localize(
                            "Errors.Conflict",
                            "Əməliyyat mövcud məlumatla ziddiyyət yaratdı.",
                            "The operation conflicts with existing data."),
                        StatusCodes.Status503ServiceUnavailable => Localize(
                            "Errors.ExternalService",
                            "Xidmət müvəqqəti olaraq əlçatan deyil.",
                            "The service is temporarily unavailable."),
                        _ => Localize(
                            "Errors.Unexpected",
                            "Gözlənilməz server xətası baş verdi.",
                            "An unexpected server error occurred.")
                    }
                };
            }

            var response = new
            {
                statusCode,
                message,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response),
                context.RequestAborted);
        }
    }

    private string Localize(string key, string azFallback, string enFallback)
    {
        var localized = _localizer[key];
        if (!localized.ResourceNotFound &&
            !string.Equals(localized.Value, key, StringComparison.Ordinal))
        {
            return localized.Value;
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("en", StringComparison.OrdinalIgnoreCase)
            ? enFallback
            : azFallback;
    }

    private static bool IsRefreshSessionSchemaMismatch(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (!message.Contains("RefreshSessions", StringComparison.OrdinalIgnoreCase))
                continue;

            if (message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Cannot find the object", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
