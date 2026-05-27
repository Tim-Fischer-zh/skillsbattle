using FluentAssertions;
using KillerSudoku.E2ETests.Fixtures;
using Microsoft.Playwright;

namespace KillerSudoku.E2ETests;

/// <summary>
/// UC03 Login-Edge-Cases — T021, T022, T024.
/// Tests gegen den laufenden Container (real ASP.NET Identity SignInManager).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class AuthLoginE2ETests
{
    private readonly PlaywrightFixture _fx;
    public AuthLoginE2ETests(PlaywrightFixture fx) => _fx = fx;

    private static string UniqueUser(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}".Substring(0, 12).ToLowerInvariant();

    private async Task RegisterAsync(IBrowserContext ctx, string username, string password)
    {
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/register");
        await page.GetByTestId("register-username").FillAsync(username);
        await page.GetByTestId("register-email").FillAsync($"{username}@e2e.local");
        await page.GetByTestId("register-password").FillAsync(password);
        await page.GetByTestId("register-password-confirm").FillAsync(password);
        await page.GetByTestId("register-submit").ClickAsync();
        await page.WaitForURLAsync(u => u.Contains("/puzzles"), new() { Timeout = 10_000 });
        await page.CloseAsync();
    }

    private async Task<IPage> AttemptLoginAsync(IBrowserContext ctx, string username, string password)
    {
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/login");
        await page.GetByTestId("login-username").FillAsync(username);
        await page.GetByTestId("login-password").FillAsync(password);
        await page.GetByTestId("login-submit").ClickAsync();
        return page;
    }

    // T021 — Falsches Passwort → generische Fehlermeldung, kein Login.
    [Fact]
    public async Task T021_WrongPassword_ShowsGenericErrorMessage()
    {
        var username = UniqueUser("wpw");
        const string realPassword = "RealPass1234";

        await using var registerCtx = await _fx.NewContextAsync();
        await RegisterAsync(registerCtx, username, realPassword);

        await using var loginCtx = await _fx.NewContextAsync();
        var page = await AttemptLoginAsync(loginCtx, username, "WrongPass1234");

        var banner = await page.GetByTestId("login-error-banner").InnerTextAsync();
        banner.Should().Contain("falsch");
        page.Url.Should().Contain("/login");
    }

    // T022 — Unknown User UND Wrong-Password liefern IDENTISCHE Message
    //   (V03 / AC03.1 — kein User-Existenz-Leak).
    [Fact]
    public async Task T022_GenericLoginError_SameForUnknownUserAndWrongPassword()
    {
        var existingUser = UniqueUser("ex");
        await using (var ctxR = await _fx.NewContextAsync())
            await RegisterAsync(ctxR, existingUser, "ValidPass123");

        // Wrong password for existing user
        await using var ctxA = await _fx.NewContextAsync();
        var pageA = await AttemptLoginAsync(ctxA, existingUser, "DefinitelyWrong");
        var msgA = await pageA.GetByTestId("login-error-banner").InnerTextAsync();

        // Non-existing user
        await using var ctxB = await _fx.NewContextAsync();
        var pageB = await AttemptLoginAsync(ctxB, "nope_" + Guid.NewGuid().ToString("N")[..6], "AnyPass1234");
        var msgB = await pageB.GetByTestId("login-error-banner").InnerTextAsync();

        msgA.Should().Be(msgB, "der Login-Fehler darf User-Existenz nicht durchsickern (V03/AC03.1)");
    }

    // T024 — Rate-Limit: nach 5 Fehlversuchen → Lockout-Message.
    //   ASP.NET Identity Lockout policy: 5 attempts / 5 min (Program.cs).
    [Fact]
    public async Task T024_RateLimit_AfterFiveFailedAttempts_ShowsLockoutMessage()
    {
        var username = UniqueUser("lock");
        await using (var ctxR = await _fx.NewContextAsync())
            await RegisterAsync(ctxR, username, "Correct12345");

        // Wir machen alle 6 Versuche im selben Browser-Context damit Lockout vom Identity-
        // Counter gesehen wird (Counter ist user-basiert, nicht context-basiert — aber im
        // selben Context bleibt es einfacher die Antwort des letzten zu inspizieren).
        await using var ctx = await _fx.NewContextAsync();
        string? lastBanner = null;
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            var page = await AttemptLoginAsync(ctx, username, $"Wrong{attempt}Pwd!");
            await page.WaitForSelectorAsync("[data-testid=login-error-banner]", new() { Timeout = 5_000 });
            lastBanner = await page.GetByTestId("login-error-banner").InnerTextAsync();
            await page.CloseAsync();
        }

        lastBanner.Should().NotBeNull();
        // Nach 5 Fehlversuchen ist der User gelockt — Versuch 6 zeigt die Lockout-Message.
        lastBanner!.Should().MatchRegex("(?i)(zu viele|lockout|5 minuten)");
    }
}
