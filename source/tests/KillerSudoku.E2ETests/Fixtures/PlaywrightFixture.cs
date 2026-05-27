using KillerSudoku.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace KillerSudoku.E2ETests.Fixtures;

/// <summary>
/// Shared Playwright + Chromium fixture for all E2E tests.
///
/// Targets the running app at <c>E2E_BASE_URL</c> (default
/// <c>http://localhost:8080</c>) — the Docker-Compose stack must already be
/// up. Browsers are downloaded on first use via the Playwright CLI.
///
/// <para>
/// For tests that need DB-state setup the fixture also exposes a
/// <see cref="SudokuDbContext"/> factory pointing at the same MS-SQL
/// container that the running app uses.
/// </para>
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl { get; }
    public string ConnectionString { get; }

    public PlaywrightFixture()
    {
        BaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL")
                  ?? "http://localhost:8080";
        var pw = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD")
                 ?? "Sudoku!Strong#Pass#2026";
        ConnectionString =
            $"Server=localhost,1433;Database=sudoku;User Id=sa;Password={pw};" +
            "Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
    }

    public async Task InitializeAsync()
    {
        // Browser-Downloads idempotent — ist Chromium schon installiert, terminiert
        // der CLI-Aufruf in < 1 s ohne erneuten Download.
        var installCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (installCode != 0)
            throw new InvalidOperationException($"playwright install chromium failed with {installCode}");

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });

        // Sanity-Check: App muss erreichbar sein, sonst macht der ganze Run keinen Sinn.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var probe = await http.GetAsync(BaseUrl);
            if (!probe.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"E2E base URL {BaseUrl} antwortete mit HTTP {(int)probe.StatusCode}.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"E2E base URL {BaseUrl} ist nicht erreichbar. " +
                $"Stack via `docker compose up -d` starten.",
                ex);
        }
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        Playwright?.Dispose();
    }

    /// <summary>
    /// Frischer <see cref="IBrowserContext"/> pro Test → isolierte Cookies,
    /// kein State-Leak zwischen Tests.
    /// </summary>
    public Task<IBrowserContext> NewContextAsync(BrowserNewContextOptions? options = null)
        => Browser.NewContextAsync(options ?? new BrowserNewContextOptions
        {
            // Cookies sollen Secure=true respektieren → ignoreHTTPSErrors true für lokales HTTP-only.
            IgnoreHTTPSErrors = true,
        });

    public SudokuDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<SudokuDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new SudokuDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Playwright";
}
