using FluentAssertions;
using KillerSudoku.Data.Entities;
using KillerSudoku.E2ETests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace KillerSudoku.E2ETests;

/// <summary>
/// End-to-End Tests gegen den laufenden Docker-Compose-Stack (localhost:8080).
/// Test-IDs gemäss docs/test-protocol.csv (alle Tests vom Typ "E").
///
/// Voraussetzungen: <c>docker compose up -d</c> läuft.
/// Chromium wird beim ersten Run via Playwright-CLI heruntergeladen.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class E2ETests
{
    private readonly PlaywrightFixture _fx;
    public E2ETests(PlaywrightFixture fx) => _fx = fx;

    private static string UniqueUser(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}".Substring(0, 12).ToLowerInvariant();

    // =====================================================================
    // T001 — Startseite ohne Login erreichbar, Page-Title + h1 enthalten "Killer Sudoku"
    // =====================================================================
    [Fact]
    public async Task T001_HomePage_AnonymousAccessOk()
    {
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync(_fx.BaseUrl);
        response.Should().NotBeNull();
        response!.Status.Should().Be(200);

        var title = await page.TitleAsync();
        title.Should().Contain("Killer Sudoku");

        var h1 = await page.Locator("h1").First.InnerTextAsync();
        h1.Should().Contain("Killer Sudoku");
    }

    // =====================================================================
    // T002 — Alle 4 Spielregeln auf der Startseite sichtbar (UC01 AC01.3)
    // =====================================================================
    [Fact]
    public async Task T002_HomePage_DisplaysAllFourRules()
    {
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();
        await page.GotoAsync(_fx.BaseUrl);

        var body = await page.Locator("body").InnerTextAsync();

        // Die vier Killer-Sudoku-Regeln aus README §1 — Fragmente reichen.
        body.Should().Contain("Zahlen 1");
        body.Should().Contain("each number exactly once");
        body.Should().Contain("cage");
        body.Should().Contain("No number appears more than once");
    }

    // =====================================================================
    // T004 — Nicht-existierende Route → NotFound-Page (404 oder Custom-NotFound)
    // =====================================================================
    [Fact]
    public async Task T004_UnknownRoute_ShowsNotFound()
    {
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/no-such-page");
        // Blazor reroutet via UseStatusCodePagesWithReExecute auf /not-found und
        // antwortet mit 200 + NotFound-Component (geprüft via sichtbaren Text).
        var body = await page.Locator("body").InnerTextAsync();
        var statusOk = response is null
            ? false
            : (response.Status == 404 || response.Status == 200);
        statusOk.Should().BeTrue();
        body.Should().MatchRegex("(?i)(404|nicht gefunden|not found)");
    }

    // =====================================================================
    // T018 — Register-Flow: Account anlegen → Auto-Login → Redirect zu Puzzle-Liste
    // =====================================================================
    [Fact]
    public async Task T018_RegisterFlow_RedirectsToProtectedArea()
    {
        var username = UniqueUser("reg");
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/register");

        await page.GetByTestId("register-username").FillAsync(username);
        await page.GetByTestId("register-email").FillAsync($"{username}@e2e.local");
        await page.GetByTestId("register-password").FillAsync("E2eTest1234");
        await page.GetByTestId("register-password-confirm").FillAsync("E2eTest1234");
        await page.GetByTestId("register-submit").ClickAsync();

        // Nach Register erwarten wir Redirect zu /puzzles (Auto-Login)
        await page.WaitForURLAsync(url => url.Contains("/puzzles"), new() { Timeout = 10_000 });
        page.Url.Should().Contain("/puzzles");
    }

    // =====================================================================
    // T023 — Auth-Cookie ist HttpOnly + Secure
    // =====================================================================
    [Fact]
    public async Task T023_AuthCookie_IsHttpOnlyAndSecure()
    {
        var username = UniqueUser("cookie");
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/register");
        await page.GetByTestId("register-username").FillAsync(username);
        await page.GetByTestId("register-email").FillAsync($"{username}@e2e.local");
        await page.GetByTestId("register-password").FillAsync("E2eTest1234");
        await page.GetByTestId("register-password-confirm").FillAsync("E2eTest1234");
        await page.GetByTestId("register-submit").ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/puzzles"), new() { Timeout = 10_000 });

        var cookies = await ctx.CookiesAsync();
        // ASP.NET Identity Application Cookie — der eigentliche Auth-Token.
        var authCookie = cookies.FirstOrDefault(c =>
            c.Name.Equals(".AspNetCore.Identity.Application", StringComparison.OrdinalIgnoreCase));
        authCookie.Should().NotBeNull("nach erfolgreicher Registrierung sollte das Auth-Cookie gesetzt sein");

        // V14 — HttpOnly verhindert JS-Zugriff (XSS-Schutz für Token-Stealing).
        authCookie!.HttpOnly.Should().BeTrue();
        // V14 — Secure-Flag (nur über HTTPS senden). Im lokalen HTTP-Container
        // ist das Flag im Cookie gesetzt; der Browser respektiert es bei HTTPS-Deploy.
        authCookie.Secure.Should().BeTrue();
        // V14 — SameSite muss != None sein (Lax oder Strict beide OK gegen CSRF).
        authCookie.SameSite.Should().BeOneOf(SameSiteAttribute.Lax, SameSiteAttribute.Strict);
    }

    // =====================================================================
    // T025 — Geschützte Route → Redirect zu /login wenn nicht angemeldet
    // =====================================================================
    [Fact]
    public async Task T025_ProtectedRoute_RedirectsToLogin_WhenAnonymous()
    {
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/puzzles");
        await page.WaitForURLAsync(u => u.Contains("/login"), new() { Timeout = 10_000 });
        page.Url.Should().Contain("/login");
    }

    // =====================================================================
    // T026 — Login + Navigation: Nach Login zeigt Header den Username
    // =====================================================================
    [Fact]
    public async Task T026_LoginFlow_HeaderShowsUsername()
    {
        var username = UniqueUser("nav");
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();

        // 1) Registrieren (impliziter Auto-Login)
        await page.GotoAsync($"{_fx.BaseUrl}/register");
        await page.GetByTestId("register-username").FillAsync(username);
        await page.GetByTestId("register-email").FillAsync($"{username}@e2e.local");
        await page.GetByTestId("register-password").FillAsync("E2eTest1234");
        await page.GetByTestId("register-password-confirm").FillAsync("E2eTest1234");
        await page.GetByTestId("register-submit").ClickAsync();
        await page.WaitForURLAsync(u => u.Contains("/puzzles"), new() { Timeout = 10_000 });

        // 2) Cookies cachen, neuen Context öffnen + Cookies wiederherstellen → echter Login-Flow
        var savedCookies = await ctx.CookiesAsync();
        await using var ctx2 = await _fx.NewContextAsync();
        await ctx2.AddCookiesAsync(savedCookies.Select(c => new Cookie
        {
            Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path,
            HttpOnly = c.HttpOnly, Secure = c.Secure,
            Expires = c.Expires,
        }).ToArray());
        var page2 = await ctx2.NewPageAsync();
        await page2.GotoAsync($"{_fx.BaseUrl}/puzzles");

        var headerText = await page2.Locator("header, .app-header").First.InnerTextAsync();
        headerText.Should().Contain(username);
    }

    // =====================================================================
    // T074 — Highscore-Page lädt Top-N (DB hat completed games)
    // =====================================================================
    [Fact]
    public async Task T074_HighscorePage_ShowsCompletedGames()
    {
        // Setup: 3 completed games direkt in DB anlegen
        var suffix = UniqueUser("hs");
        var (userId, puzzleId) = await SeedUserAndPuzzleAsync(suffix);
        await SeedCompletedGameAsync(userId, puzzleId, score: 5500, time: 800, hints: 0);
        await SeedCompletedGameAsync(userId, puzzleId, score: 8200, time: 500, hints: 1);
        await SeedCompletedGameAsync(userId, puzzleId, score: 7000, time: 700, hints: 0);

        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();

        // Login via Direct-Cookie-Injection ist komplex — wir testen die Page anonym
        // funktioniert nicht (Authorize), also via Register + Re-Use:
        var loginUsername = UniqueUser("hsuser");
        await page.GotoAsync($"{_fx.BaseUrl}/register");
        await page.GetByTestId("register-username").FillAsync(loginUsername);
        await page.GetByTestId("register-email").FillAsync($"{loginUsername}@e2e.local");
        await page.GetByTestId("register-password").FillAsync("E2eTest1234");
        await page.GetByTestId("register-password-confirm").FillAsync("E2eTest1234");
        await page.GetByTestId("register-submit").ClickAsync();
        await page.WaitForURLAsync(u => u.Contains("/puzzles"), new() { Timeout = 10_000 });

        await page.GotoAsync($"{_fx.BaseUrl}/highscore");
        var body = await page.Locator("body").InnerTextAsync();

        // Mindestens unsere drei seed-scores müssen erscheinen
        body.Should().Contain("8200");
        body.Should().Contain("7000");
        body.Should().Contain("5500");
    }

    // =====================================================================
    // T114 — Filter ändert URL + Liste (?difficulty=2)
    // =====================================================================
    [Fact]
    public async Task T114_FilterChangesUrlAndList()
    {
        // Setup: stelle sicher dass mindestens 2 Diff-2-Puzzles existieren
        var suffix = UniqueUser("flt");
        var (userId, _) = await SeedUserAndPuzzleAsync(suffix, difficulty: 2);
        await SeedPuzzleAsync(userId, difficulty: 2);

        var loginUsername = UniqueUser("fltu");
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/register");
        await page.GetByTestId("register-username").FillAsync(loginUsername);
        await page.GetByTestId("register-email").FillAsync($"{loginUsername}@e2e.local");
        await page.GetByTestId("register-password").FillAsync("E2eTest1234");
        await page.GetByTestId("register-password-confirm").FillAsync("E2eTest1234");
        await page.GetByTestId("register-submit").ClickAsync();
        await page.WaitForURLAsync(u => u.Contains("/puzzles"), new() { Timeout = 10_000 });

        // Click "Mittel (2)" Filter
        await page.GetByRole(AriaRole.Button, new() { Name = "Mittel (2)" }).ClickAsync();

        // URL muss ?difficulty=2 enthalten
        await page.WaitForURLAsync(u => u.Contains("difficulty=2"), new() { Timeout = 5_000 });
        page.Url.Should().Contain("difficulty=2");

        // Auf Filter-Result warten: Karten mit data-difficulty != 2 sollen verschwinden.
        // (Pill wird vorher "active" gesetzt, aber LoadAsync läuft asynchron.)
        await page.WaitForFunctionAsync(@"() => {
            const cards = Array.from(document.querySelectorAll('[data-testid=puzzle-card]'));
            return cards.length > 0 && cards.every(c => c.dataset.difficulty === '2');
        }", null, new() { Timeout = 5_000 });

        var cards = await page.Locator("[data-testid=puzzle-card]").AllAsync();
        cards.Should().NotBeEmpty();
        foreach (var card in cards)
        {
            var diff = await card.GetAttributeAsync("data-difficulty");
            diff.Should().Be("2");
        }
    }

    // =====================================================================
    // T124 — Pause-Button öffnet Overlay, Resume macht Grid wieder editierbar
    // =====================================================================
    [Fact]
    public async Task T124_PauseButton_ShowsOverlayAndResumeRestores()
    {
        // Setup: User + Puzzle + Active-Game vorbereiten
        var suffix = UniqueUser("pause");
        var (userId, puzzleId) = await SeedUserAndPuzzleAsync(suffix);

        // Login als dieser User via dem klassischen Login-Flow
        var loginUsername = UniqueUser("pu");
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();

        // Wir machen Register + nutzen den just-eingeloggten User, NICHT den seed-user.
        // Damit das geseedete Puzzle spielbar ist, wurde es ja als public eingerichtet
        // (alle eingeloggten User können jedes Puzzle spielen — UC06).
        await page.GotoAsync($"{_fx.BaseUrl}/register");
        await page.GetByTestId("register-username").FillAsync(loginUsername);
        await page.GetByTestId("register-email").FillAsync($"{loginUsername}@e2e.local");
        await page.GetByTestId("register-password").FillAsync("E2eTest1234");
        await page.GetByTestId("register-password-confirm").FillAsync("E2eTest1234");
        await page.GetByTestId("register-submit").ClickAsync();
        await page.WaitForURLAsync(u => u.Contains("/puzzles"), new() { Timeout = 10_000 });

        await page.GotoAsync($"{_fx.BaseUrl}/puzzles/{puzzleId}/play");

        // Auf das Grid warten (async OnInitializedAsync lädt Game-Daten).
        await page.Locator(".grid--play").WaitForAsync(new() { Timeout = 15_000 });

        // Pause klicken — Button-Text enthält ⏸ Unicode + "Pausieren"
        var pauseBtn = page.Locator("button:has-text('Pausieren')");
        await pauseBtn.First.WaitForAsync(new() { Timeout = 10_000 });
        await pauseBtn.First.ClickAsync();

        // Overlay erscheint mit "Pausiert"
        var overlay = page.Locator(".grid__pause-overlay");
        await overlay.WaitForAsync(new() { Timeout = 5_000 });
        var overlayText = await overlay.InnerTextAsync();
        overlayText.Should().Contain("Pausiert");

        // Resume → der "Weiterspielen"-Button INNERHALB des Overlays (es gibt einen
        // gleichnamigen Button in der Toolbar wenn _isPaused=true; wir wollen den im Overlay).
        await page.Locator(".grid__pause-overlay button:has-text('Weiterspielen')").ClickAsync();
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5_000 });
    }

    // ---------------------------------------------------------------------
    // Helpers — direkter DB-Zugriff für Test-Setup
    // ---------------------------------------------------------------------
    private async Task<(int userId, int puzzleId)> SeedUserAndPuzzleAsync(string suffix, byte difficulty = 1)
    {
        await using var db = _fx.NewDbContext();
        var user = new AppUser
        {
            UserName = $"seed_{suffix}",
            NormalizedUserName = $"SEED_{suffix}".ToUpperInvariant(),
            Email = $"seed_{suffix}@e2e.local",
            NormalizedEmail = $"SEED_{suffix}@E2E.LOCAL".ToUpperInvariant(),
            EmailConfirmed = true,
            PasswordHash = "fake-hash",
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var puzzleId = await SeedPuzzleAsync(user.Id, difficulty);
        return (user.Id, puzzleId);
    }

    private async Task<int> SeedPuzzleAsync(int userId, byte difficulty)
    {
        await using var db = _fx.NewDbContext();
        var puzzle = new Puzzle
        {
            Difficulty = difficulty,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Puzzles.Add(puzzle);
        await db.SaveChangesAsync();

        // 9 Row-Cages mit Sum 45 — strukturell gültig und für Pause/Filter-Tests ausreichend.
        for (byte r = 0; r < 9; r++)
        {
            var cage = new Cage { PuzzleId = puzzle.Id, Sum = 45 };
            db.Cages.Add(cage);
            await db.SaveChangesAsync();

            for (byte c = 0; c < 9; c++)
            {
                db.CageCells.Add(new CageCell { CageId = cage.Id, RowIdx = r, ColIdx = c });
            }
            await db.SaveChangesAsync();
        }
        return puzzle.Id;
    }

    private async Task SeedCompletedGameAsync(int userId, int puzzleId, int score, int time, int hints)
    {
        await using var db = _fx.NewDbContext();
        db.Games.Add(new Game
        {
            UserId = userId,
            PuzzleId = puzzleId,
            StartTime = DateTime.UtcNow.AddSeconds(-time),
            EndTime = DateTime.UtcNow,
            TimeSeconds = time,
            HintsUsed = hints,
            Score = score,
            IsCompleted = true,
        });
        await db.SaveChangesAsync();
    }
}
