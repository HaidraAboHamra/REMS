using Blazorise;
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


builder.Services.AddRazorPages();
builder.Services.AddMudServices();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.WebHost.UseUrls("http://0.0.0.0:2004");
string myPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "REMS.db");
//test
builder.Services.AddDbContext<AppDbContext>(Option =>
    Option.UseSqlite($"Data Source={myPath}"));
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddSingleton<EmailService>();
builder.Services.AddHostedService<ReportEmailHostedService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TelegramService>();
builder.Services.AddHostedService<TelegramMessageScheduler>();
builder.Services.AddScoped<IAuthentication, AuthenticationRepository>();
builder.Services.AddScoped<IFollowUpReportService, FollowUpReportService>();
builder.Services.AddScoped<ISettings, SettingsRepository>();
builder.Services.AddScoped<ExcelService>();
builder.Services
    .AddBlazorise(options =>
    {
        options.Immediate = true;
    });

builder.Services.AddScoped<Test>();
builder.Services.AddHttpContextAccessor();

/*builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme,
        options =>
{
    options.LoginPath = new PathString("/login");
    options.AccessDeniedPath = new PathString("/auth/denied");
});
builder.Services.AddControllers();
builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    });
*/


builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Application"; // Default scheme for the app
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddPolicyScheme("Application", "JWT or Cookie", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // Use JWT Bearer for API requests
        if (context.Request.Path.StartsWithSegments("/api"))
            return JwtBearerDefaults.AuthenticationScheme;

        // Use Cookie for non-API requests
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ValidateIssuerSigningKey = true,
        ValidateAudience = false,
        ValidateIssuer = false,
        RequireExpirationTime=false,
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(20);
});



builder.Services.AddCors(options => options.AddPolicy("AlowAll", builder =>
    builder.AllowAnyOrigin()
    .AllowAnyHeader().
    AllowAnyOrigin()
));

builder.Services.AddControllers();
var app = builder.Build();
app.UseCors("AlowAll");
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.MapRazorPages();
app.MapFallbackToPage("/login");
app.UseAntiforgery();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
