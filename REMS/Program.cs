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
using REMS.Authorization;
using REMS.Data;
using REMS.Infrastructure;
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
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 120;
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
var dbPath = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefultConnection")
    ?? throw new InvalidOperationException("A database connection string is required.");

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
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddHostedService<AuditLogCleanupService>();

builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddSingleton<ProfileImageService>();

var telegramToken = builder.Configuration["TelegramBotToken"];
if (!string.IsNullOrWhiteSpace(telegramToken))
{
    builder.Services.AddSingleton<TelegramService>();
    builder.Services.AddHostedService<TelegramMessageScheduler>();
    builder.Services.AddHostedService<TelegramBotHostedService>();
}

builder.Services.AddScoped<IAuthentication, AuthenticationRepository>();
builder.Services.AddScoped<IFollowUpReportService, FollowUpReportService>();
builder.Services.AddScoped<ISettings, SettingsRepository>();
builder.Services.AddScoped<AdminCenterService>();
builder.Services.AddScoped<TelegramAdminService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<Test>();
builder.Services.AddHttpContextAccessor();
// ======================================================
// Authentication & Authorization
// ======================================================

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (builder.Environment.IsDevelopment())
        jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
    else
        throw new InvalidOperationException("JWT key is missing. Configure it with user-secrets or an environment variable in production.");
}

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT key must contain at least 32 bytes.");
}

// Keep the generated development key available to LoginController as well.
// Production still requires Jwt:Key from an external secret provider.
builder.Configuration["Jwt:Key"] = jwtKey;

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
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
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
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.Name = "REMS.Auth";
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminCenter", policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPermissions.DashboardView)));
    options.AddPolicy("AdminPermissions", policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPermissions.SensitiveActions)));
    options.AddPolicy("AdminTelegram", policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPermissions.TelegramManage)));
    options.AddPolicy("AdminUsers", policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPermissions.UsersManage)));
    options.AddPolicy("AdminUserStatus", policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPermissions.UserStatusManage)));
    options.AddPolicy("AdminSettings", policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPermissions.SettingsManage)));
    options.AddPolicy("AdminHealth", policy =>
        policy.Requirements.Add(new AdminPermissionRequirement(AdminPermissions.HealthView)));
});
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AdminPermissionHandler>();

// ======================================================
// Build
// ======================================================

var app = builder.Build();

// Keep the local SQLite schema aligned with the application on first startup.
// This is especially important for desktop deployments where migrations are not run separately.
await app.Services.InitializeRemsDatabaseAsync(builder.Configuration, app.Environment.IsDevelopment());

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

app.UseRemsSecurityHeaders(app.Environment);

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// ======================================================
// Endpoints
// ======================================================

app.MapControllers().RequireRateLimiting("api");

app.MapRazorPages().RequireRateLimiting("login");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapFallbackToPage("/login");

// ======================================================
// Run
// ======================================================

app.Run();
