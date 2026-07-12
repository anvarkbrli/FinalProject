using Ludora.Application.Exceptions;
using System.Text.Json;

namespace Ludora.API.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next,ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,"Unhandled exception occurred.");

                context.Response.ContentType = "application/json";

                context.Response.StatusCode = exception switch
                {
                    BadRequestException => StatusCodes.Status400BadRequest,

                    ArgumentException => StatusCodes.Status400BadRequest,

                    UnauthorizedException => StatusCodes.Status401Unauthorized,
                    
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,

                    ForbiddenException => StatusCodes.Status403Forbidden,

                    NotFoundException => StatusCodes.Status404NotFound,

                    KeyNotFoundException => StatusCodes.Status404NotFound,

                    ConflictException => StatusCodes.Status409Conflict,

                    _ => StatusCodes.Status500InternalServerError
                };

                var response = new
                {
                    statusCode = context.Response.StatusCode,

                    message = exception.Message
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }
    }
}
