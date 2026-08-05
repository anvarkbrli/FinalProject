using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ludora.API.HealthChecks;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static Task WritePublicAsync(HttpContext context, HealthReport report)
    {
        PrepareResponse(context);

        var payload = new
        {
            status = report.Status.ToString(),
            traceId = context.TraceIdentifier
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static Task WriteDetailedAsync(HttpContext context, HealthReport report)
    {
        PrepareResponse(context);

        var payload = new
        {
            status = report.Status.ToString(),
            traceId = context.TraceIdentifier,
            durationMilliseconds = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries.ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    status = pair.Value.Status.ToString(),
                    description = pair.Value.Description,
                    durationMilliseconds = Math.Round(pair.Value.Duration.TotalMilliseconds, 2),
                    data = pair.Value.Data
                })
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static void PrepareResponse(HttpContext context)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers["Cache-Control"] = "no-store, no-cache";
        context.Response.Headers["Pragma"] = "no-cache";
    }
}
