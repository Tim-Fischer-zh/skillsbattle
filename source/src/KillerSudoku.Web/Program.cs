using KillerSudoku.Core.Abstractions;
using KillerSudoku.Core.Services;
using KillerSudoku.Data.Entities;
using KillerSudoku.Data.Persistence;
using KillerSudoku.Data.Services;
using KillerSudoku.Web.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core — MS-SQL via "Sudoku" connection string
builder.Services.AddDbContext<SudokuDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Sudoku")));

// ASP.NET Core Identity — Cookie-Auth, PBKDF2-Hashing default
builder.Services.AddIdentity<AppUser, IdentityRole<int>>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedEmail = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<SudokuDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.LoginPath = "/login";
    o.AccessDeniedPath = "/access-denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(2);
});

builder.Services.AddAuthorization();

// Domain (pure, no IO) — Singleton
builder.Services.AddSingleton<IScoreCalculator, ScoreCalculator>();
builder.Services.AddSingleton<ISolverService, SolverService>();
builder.Services.AddSingleton<SolutionValidator>();
builder.Services.AddSingleton<PuzzleStructureValidator>();
builder.Services.AddSingleton<IPuzzleGenerator, PuzzleGenerator>();

// Application Services — Scoped (per request/circuit, share DbContext)
builder.Services.AddScoped<IPuzzleService, PuzzleService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IHintService, HintService>();
builder.Services.AddScoped<IHighscoreService, HighscoreService>();
builder.Services.AddScoped<PuzzleSeeder>();

// TimeProvider for testability
builder.Services.AddSingleton(TimeProvider.System);

// Data-Protection-Keys persistieren, damit Antiforgery-Tokens Container-Restarts überleben.
// In Production (Container) wird ein writable Volume genutzt; sonst Standard-Pfad.
var dpKeyDir = Environment.GetEnvironmentVariable("DATAPROTECTION_KEYS_DIR")
    ?? (builder.Environment.IsProduction() ? "/var/opt/mssql/.dpkeys" : null);
if (!string.IsNullOrEmpty(dpKeyDir))
{
    Directory.CreateDirectory(dpKeyDir);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dpKeyDir))
        .SetApplicationName("KillerSudoku");
}

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// HTTPS-Redirection nur, wenn HTTPS-Listener vorhanden ist (Dev / Reverse-Proxy).
// Im Container läuft Kestrel auf reinem HTTP :8080 — Redirect würde 500 produzieren.
if (!string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_HTTPS_PORTS"])
    || (builder.Configuration["ASPNETCORE_URLS"] ?? "").Contains("https://"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// UC03 — Logout endpoint, posted to by the header form in MainLayout
// (Antiforgery is enforced by app.UseAntiforgery() + <AntiforgeryToken /> in the form).
app.MapPost("/logout", async (SignInManager<AppUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
});

// Optional puzzle seeding — runs once and exits when the `seed` argument is given,
// e.g. `dotnet KillerSudoku.Web.dll seed [count]` or env-var `SEED_PUZZLES=5`.
if (args.Length > 0 && string.Equals(args[0], "seed", StringComparison.OrdinalIgnoreCase))
{
    int count = 5;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        count = parsed;
    await RunSeederAsync(app, count);
    return;
}
if (int.TryParse(Environment.GetEnvironmentVariable("SEED_PUZZLES"), out var envCount) && envCount > 0)
{
    await RunSeederAsync(app, envCount);
    // Continue startup — the web app runs alongside, useful for the production container.
}

app.Run();

static async Task RunSeederAsync(WebApplication app, int count)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<PuzzleSeeder>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<PuzzleSeeder>>();
    log.LogInformation("Starting puzzle seeder: {Count} puzzles per difficulty ...", count);
    var created = await seeder.SeedAsync(count);
    log.LogInformation("Seeder completed — created {Created} puzzles total.", created);
}

public partial class Program;
