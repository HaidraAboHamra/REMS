using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MudBlazor.Services;
using REMS.Components;
using REMS.Data;
using REMS.Interfaces;
using REMS.Services;
using ReportApp.Services;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// The Windows Event Log is frequently unavailable to desktop users and should
// never prevent the web host from starting. Console/debug providers are enough
// for the local application and preserve useful diagnostics.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ======================================================
// Configure Services
// ======================================================

builder.Services.AddRazorPages();

var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("REMS");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services
    .AddBlazorise(options =>
    {
        options.Immediate = true;
    })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

// ======================================================
// Kestrel
// ======================================================

builder.WebHost.UseUrls("http://127.0.0.1:2004");

// ======================================================
// Database
// ======================================================

//const string dbPath = "/var/lib/rems/REMS.db";

//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseSqlite($"Data Source={dbPath}"));
var dbPath =
    builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(dbPath));
// ======================================================
// Dependency Injection
// ======================================================

builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddSingleton<EmailService>();

builder.Services.AddHostedService<ReportEmailHostedService>();
builder.Services.AddHostedService<LateTaskPenaltyService>();

builder.Services.AddScoped<UserService>();

builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddSingleton<TelegramService>();

builder.Services.AddHostedService<TelegramMessageScheduler>();
builder.Services.AddHostedService<TelegramBotHostedService>();

builder.Services.AddScoped<IAuthentication, AuthenticationRepository>();
builder.Services.AddScoped<IFollowUpReportService, FollowUpReportService>();
builder.Services.AddScoped<ISettings, SettingsRepository>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<Test>();
builder.Services.AddHttpContextAccessor();
// ======================================================
// Authentication & Authorization
// ======================================================

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT key is missing. Configure Jwt:Key in production.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "Application";
        options.DefaultSignInScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddPolicyScheme("Application", "JWT or Cookie", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
                return JwtBearerDefaults.AuthenticationScheme;

            return CookieAuthenticationDefaults.AuthenticationScheme;
        };
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),

            ValidateIssuerSigningKey = true,
            ValidateAudience = false,
            ValidateIssuer = false,
            RequireExpirationTime = false
        };
    })
    .AddCookie(
        CookieAuthenticationDefaults.AuthenticationScheme,
        options =>
        {
            options.LoginPath = "/login";
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

// ======================================================
// CORS
// ======================================================

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    options.AddPolicy("AppOnly", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                return uri.IsLoopback || allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

// ======================================================
// Build
// ======================================================

var app = builder.Build();

// Keep the local SQLite schema aligned with the application on first startup.
// This is especially important for desktop deployments where migrations are not run separately.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

// ======================================================
// Reverse Proxy / Nginx
// ======================================================

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});

// ======================================================
// CORS
// ======================================================

app.UseCors("AppOnly");

// ======================================================
// Middleware Pipeline
// ======================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), unload=*");
    await next();
});

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// ======================================================
// Endpoints
// ======================================================

app.MapControllers();

app.MapRazorPages().RequireRateLimiting("login");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapFallbackToPage("/login");

// ======================================================
// Run
// ======================================================

app.Run();
