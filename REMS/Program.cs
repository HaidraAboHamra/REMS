using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
// ✅ Configure Services
// ======================================================

// Add Razor Pages (needed for /login, /Error, etc.)
builder.Services.AddRazorPages();

// Add Razor Components (for Blazor Server)
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// Blazorise setup
builder.Services
    .AddBlazorise(options =>
    {
        options.Immediate = true;
    })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

builder.WebHost.UseUrls("http://0.0.0.0:2004");

// ======================================================
// ✅ Database (SQLite in wwwroot)
// ======================================================
string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "REMS.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ======================================================
// ✅ Dependency Injection
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
builder.Services.AddHttpContextAccessor();

// ======================================================
// ✅ Authentication & Authorization
// ======================================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Application";
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SuperSecretKey12345")),
        ValidateIssuerSigningKey = true,
        ValidateAudience = false,
        ValidateIssuer = false,
        RequireExpirationTime = false,
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(20);
});

// ======================================================
// ✅ CORS + Controllers
// ======================================================
builder.Services.AddCors(options => options.AddPolicy("AllowAll",
    policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddControllers();

// ======================================================
// ✅ Build the app
// ======================================================
var app = builder.Build();

app.UseCors("AllowAll");

// ======================================================
// ✅ Middleware Pipeline
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
// ✅ Map Endpoints
// ======================================================
app.MapControllers();
app.MapRazorPages();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapFallbackToPage("/login");

// ======================================================
// ✅ Run the app
// ======================================================
app.Run();
