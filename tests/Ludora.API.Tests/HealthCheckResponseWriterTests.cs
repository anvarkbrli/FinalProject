using System.Text;

using Ludora.API.HealthChecks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ludora.API.Tests
{
    public sealed class HealthCheckResponseWriterTests
    {
        [Fact]
        public async Task PublicResponse_DoesNotExposeCheckData()
        {
            var context = CreateContext();
            var report = CreateReport();

            await HealthCheckResponseWriter.WritePublicAsync(context, report);
            var body = await ReadBodyAsync(context);

            Assert.Contains("Healthy", body, StringComparison.Ordinal);
            Assert.DoesNotContain("checks", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/data/wellplayd/private", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DetailedResponse_ContainsCheckData()
        {
            var context = CreateContext();
            var report = CreateReport();

            await HealthCheckResponseWriter.WriteDetailedAsync(context, report);
            var body = await ReadBodyAsync(context);

            Assert.Contains("storage", body, StringComparison.Ordinal);
            Assert.Contains("/data/wellplayd/private", body, StringComparison.Ordinal);
        }

        private static DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static HealthReport CreateReport()
        {
            var entry = new HealthReportEntry(
                HealthStatus.Healthy,
                "Storage is writable.",
                TimeSpan.FromMilliseconds(5),
                exception: null,
                data: new Dictionary<string, object>
                {
                    ["privatePath"] = "/data/wellplayd/private"
                },
                tags: ["ready"]);

            return new HealthReport(
                new Dictionary<string, HealthReportEntry>
                {
                    ["storage"] = entry
                },
                TimeSpan.FromMilliseconds(5));
        }

        private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(
                context.Response.Body,
                Encoding.UTF8,
                leaveOpen: true);
            return await reader.ReadToEndAsync();
        }
    }
}
