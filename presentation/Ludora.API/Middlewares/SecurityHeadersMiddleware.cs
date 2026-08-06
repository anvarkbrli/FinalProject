namespace Ludora.API.Middlewares
{
    public sealed class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] =
                    "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                headers["Cross-Origin-Resource-Policy"] =
                    context.Request.Path.StartsWithSegments("/uploads")
                        ? "cross-origin"
                        : "same-site";

                if (!context.Request.Path.StartsWithSegments("/swagger"))
                {
                    headers["Content-Security-Policy"] =
                        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
                }

                if (context.Request.Path.StartsWithSegments("/api/auth"))
                {
                    headers["Cache-Control"] = "no-store, no-cache";
                    headers["Pragma"] = "no-cache";
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
