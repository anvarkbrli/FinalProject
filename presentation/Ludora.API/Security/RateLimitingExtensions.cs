using System.Security.Claims;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Security
{
    public static class RateLimitingExtensions
    {
        public static IServiceCollection AddWellPlaydRateLimiting(
            this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var path = context.Request.Path;
                    if (path.StartsWithSegments("/swagger") ||
                        path.StartsWithSegments("/health") ||
                        path.StartsWithSegments("/hubs"))
                    {
                        return RateLimitPartition.GetNoLimiter("excluded");
                    }

                    var key = GetPartitionKey(context, "global");
                    var isRead = HttpMethods.IsGet(context.Request.Method) ||
                                 HttpMethods.IsHead(context.Request.Method) ||
                                 HttpMethods.IsOptions(context.Request.Method);

                    return isRead
                        ? RateLimitPartition.GetFixedWindowLimiter(
                            key,
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 300,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            })
                        : RateLimitPartition.GetTokenBucketLimiter(
                            key,
                            _ => new TokenBucketRateLimiterOptions
                            {
                                TokenLimit = 120,
                                TokensPerPeriod = 120,
                                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            });
                });

                options.OnRejected = async (context, cancellationToken) =>
                {
                    var retryAfterSeconds = 0;
                    if (context.Lease.TryGetMetadata(
                            MetadataName.RetryAfter,
                            out var retryAfter))
                    {
                        retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                        context.HttpContext.Response.Headers["Retry-After"] =
                            retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

                    context.HttpContext.Response.ContentType =
                        "application/json; charset=utf-8";
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new
                        {
                            statusCode = StatusCodes.Status429TooManyRequests,
                            message = "Çox sayda sorğu göndərmisiniz. Bir qədər sonra yenidən cəhd edin.",
                            retryAfterSeconds,
                            traceId = context.HttpContext.TraceIdentifier
                        },
                        cancellationToken);
                };

                options.AddPolicy(
                    RateLimitPolicyNames.Auth,
                    context => FixedWindow(context, "auth", 10, TimeSpan.FromMinutes(1), false));
                options.AddPolicy(
                    RateLimitPolicyNames.EmailAuth,
                    context => FixedWindow(context, "email-auth", 5, TimeSpan.FromMinutes(15), false));
                options.AddPolicy(
                    RateLimitPolicyNames.AccountSensitive,
                    context => FixedWindow(context, "account", 8, TimeSpan.FromMinutes(10), true));
                options.AddPolicy(
                    RateLimitPolicyNames.Uploads,
                    context => TokenBucket(context, "upload", 20, 10, TimeSpan.FromMinutes(5), true));
                options.AddPolicy(
                    RateLimitPolicyNames.ForumWrite,
                    context => TokenBucket(context, "forum-write", 30, 30, TimeSpan.FromMinutes(1), true));
                options.AddPolicy(
                    RateLimitPolicyNames.SocialWrite,
                    context => TokenBucket(context, "social-write", 90, 45, TimeSpan.FromSeconds(30), true));
                options.AddPolicy(
                    RateLimitPolicyNames.Moderation,
                    context => FixedWindow(context, "moderation", 60, TimeSpan.FromMinutes(1), true));
                options.AddPolicy(
                    RateLimitPolicyNames.UserReports,
                    context => FixedWindow(context, "user-report", 20, TimeSpan.FromHours(1), true));
                options.AddPolicy(
                    RateLimitPolicyNames.FpsEstimator,
                    context => FixedWindow(context, "fps", 5, TimeSpan.FromMinutes(1), true));
                options.AddPolicy(
                    RateLimitPolicyNames.SteamSync,
                    context => FixedWindow(context, "steam", 10, TimeSpan.FromMinutes(1), true));
                options.AddPolicy(
                    RateLimitPolicyNames.Search,
                    context => TokenBucket(context, "search", 60, 30, TimeSpan.FromSeconds(30), true));
                options.AddPolicy(
                    RateLimitPolicyNames.Chat,
                    context => TokenBucket(context, "chat", 30, 15, TimeSpan.FromSeconds(10), true));
                options.AddPolicy(
                    RateLimitPolicyNames.Contact,
                    context => FixedWindow(context, "contact", 5, TimeSpan.FromHours(1), true));
            });

            return services;
        }

        private static RateLimitPartition<string> FixedWindow(
            HttpContext context,
            string prefix,
            int permitLimit,
            TimeSpan window,
            bool preferUser) =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetPartitionKey(context, prefix, preferUser),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });

        private static RateLimitPartition<string> TokenBucket(
            HttpContext context,
            string prefix,
            int tokenLimit,
            int tokensPerPeriod,
            TimeSpan replenishmentPeriod,
            bool preferUser) =>
            RateLimitPartition.GetTokenBucketLimiter(
                GetPartitionKey(context, prefix, preferUser),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = tokenLimit,
                    TokensPerPeriod = tokensPerPeriod,
                    ReplenishmentPeriod = replenishmentPeriod,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });

        private static string GetPartitionKey(
            HttpContext context,
            string prefix,
            bool preferUser = true)
        {
            var userId = preferUser
                ? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;
            var identity = !string.IsNullOrWhiteSpace(userId)
                ? $"user:{userId}"
                : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
            return $"{prefix}:{identity}";
        }
    }
}
