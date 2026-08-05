using System.Diagnostics;

namespace Ludora.API.Middlewares;

public sealed class RequestTelemetryMiddleware
{
    private const int SlowRequestThresholdMilliseconds = 1000;
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTelemetryMiddleware> _logger;

    public RequestTelemetryMiddleware(
        RequestDelegate next,
        ILogger<RequestTelemetryMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var statusCode = context.Response.StatusCode;

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(
                    "HTTP {Method} {Path} returned {StatusCode} in {ElapsedMilliseconds} ms. TraceId: {TraceId}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    statusCode,
                    Math.Round(elapsed.TotalMilliseconds, 2),
                    context.TraceIdentifier);
            }
            else if (statusCode >= StatusCodes.Status400BadRequest ||
                     elapsed.TotalMilliseconds >= SlowRequestThresholdMilliseconds)
            {
                _logger.LogWarning(
                    "HTTP {Method} {Path} returned {StatusCode} in {ElapsedMilliseconds} ms. TraceId: {TraceId}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    statusCode,
                    Math.Round(elapsed.TotalMilliseconds, 2),
                    context.TraceIdentifier);
            }
            else
            {
                _logger.LogDebug(
                    "HTTP {Method} {Path} returned {StatusCode} in {ElapsedMilliseconds} ms. TraceId: {TraceId}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    statusCode,
                    Math.Round(elapsed.TotalMilliseconds, 2),
                    context.TraceIdentifier);
            }
        }
    }
}
