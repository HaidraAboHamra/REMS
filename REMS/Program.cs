using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MudBlazor.Services;
using REMS.Components;
using REMS.Data;
using REMS.Interfaces;
using REMS.Services;
using ReportApp.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// Configure Services
// ======================================================

builder.Services.AddRazorPages();

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

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TelegramService>();

builder.Services.AddHostedService<TelegramMessageScheduler>();

builder.Services.AddScoped<IAuthentication, AuthenticationRepository>();
builder.Services.AddScoped<IFollowUpReportService, FollowUpReportService>();
builder.Services.AddScoped<ISettings, SettingsRepository>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<Test>();
builder.Services.AddScoped<FileStorageService>();
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
            options.ExpireTimeSpan = TimeSpan.FromDays(20);
        });

// ======================================================
// CORS
// ======================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

// ======================================================
// Build
// ======================================================

var app = builder.Build();

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

app.UseCors("AllowAll");

// ======================================================
// Middleware Pipeline
// ======================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// ======================================================
// Endpoints
// ======================================================

app.MapControllers();

app.MapRazorPages();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapFallbackToPage("/login");

// ======================================================
// Run
// ======================================================

app.Run();