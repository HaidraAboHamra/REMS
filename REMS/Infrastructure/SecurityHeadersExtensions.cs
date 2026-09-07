namespace REMS.Infrastructure;

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseRemsSecurityHeaders(
        this IApplicationBuilder app,
        IWebHostEnvironment environment)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
            context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            // `unload` is deprecated and causes a browser warning when it is
            // explicitly disabled. The application does not use those APIs.
            context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

            var browserLinkConnectSource = environment.IsDevelopment()
                ? " http://localhost:*"
                : string.Empty;

            context.Response.Headers.TryAdd(
                "Content-Security-Policy",
                $"default-src 'self'; img-src 'self' data: https:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; connect-src 'self' ws: wss:{browserLinkConnectSource}; frame-ancestors 'self'; base-uri 'self'; form-action 'self'");

            await next();
        });
    }
}
