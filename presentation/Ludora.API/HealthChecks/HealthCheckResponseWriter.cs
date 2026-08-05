using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ludora.API.HealthChecks;

public static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
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
            JsonSerializer.Serialize(
                payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
