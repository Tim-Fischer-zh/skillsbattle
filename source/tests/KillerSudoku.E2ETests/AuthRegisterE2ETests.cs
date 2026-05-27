using FluentAssertions;
using KillerSudoku.E2ETests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.E2ETests;

/// <summary>
/// UC02 Register-Validation — End-to-End-Tests gegen den laufenden Container.
/// Tests T010..T017 (Code-relevant, ohne T018 = bereits in E2ETests.cs).
/// Per CSV als "I/xUnit+WAF" geplant — wir testen den vollständigen Stack
/// inklusive ASP.NET Identity über echte HTTP-Requests/Browser-Klicks, weil
/// es keinen eigenen IAuthService-Wrapper gibt (ADR-001).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class AuthRegisterE2ETests
{
    private readonly PlaywrightFixture _fx;
    public AuthRegisterE2ETests(PlaywrightFixture fx) => _fx = fx;

    private static string UniqueUser(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}".Substring(0, 12).ToLowerInvariant();

    private async Task FillAndSubmitAsync(Microsoft.Playwright.IPage page,
        string username, string email, string password, string passwordConfirm)
    {
        await page.GotoAsync($"{_fx.BaseUrl}/register");
        await page.GetByTestId("register-username").FillAsync(username);
        await page.GetByTestId("register-email").FillAsync(email);
        await page.GetByTestId("register-password").FillAsync(password);
        await page.GetByTestId("register-password-confirm").FillAsync(passwordConfirm);
        await page.GetByTestId("register-submit").ClickAsync();
    }

    // T010 — Positive Registrierung: AppUser-Row in DB, PasswordHash ist gehashed.
    [Fact]
    public async Task T010_ValidRegistration_PersistsUserWithHashedPassword()
    {
        var username = UniqueUser("ok");
        var email = $"{username}@e2e.local";
        const string password = "ValidPass123";

        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();
        await FillAndSubmitAsync(page, username, email, password, password);
        await page.WaitForURLAsync(u => u.Contains("/puzzles"), new() { Timeout = 10_000 });

        await using var db = _fx.NewDbContext();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.UserName == username);
        user.Email.Should().Be(email);
        user.PasswordHash.Should().NotBeNullOrEmpty();
        user.PasswordHash.Should().NotContain(password);
    }

    // T011 — Username zu kurz → Validation-Error sichtbar, kein Insert.
    [Fact]
    public async Task T011_UsernameTooShort_ShowsValidationError()
    {
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();
        await FillAndSubmitAsync(page, "ab", "a@b.de", "ValidPass123", "ValidPass123");

        await page.WaitForSelectorAsync(".form-error", new() { Timeout = 5_000 });
        var errors = await page.Locator(".form-error").AllInnerTextsAsync();
        errors.Should().NotBeEmpty();
        page.Url.Should().Contain("/register");
    }

    // T012 — Boundary: 3 Zeichen OK, 2 reject, 50 OK, 51 reject.
    //   Username muss EXAKT die getestete Länge haben — wir verwenden Pattern
    //   "a + 1..N-1 alphanumerisch" damit Uniqueness garantiert und nur Length variiert.
    [Theory]
    [InlineData(3, true)]
    [InlineData(50, true)]
    [InlineData(2, false)]
    [InlineData(51, false)]
    public async Task T012_UsernameLengthBoundaries(int len, bool shouldSucceed)
    {
        // Eindeutigen Username genau dieser Länge erzeugen (bis zu 51 Zeichen lang).
        var rnd = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"); // 64 chars
        var username = ("a" + rnd).Substring(0, len);

        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();
        // Email is generated separately so its length doesn't depend on username.
        var email = $"u{Guid.NewGuid():N}".Substring(0, 12) + "@e2e.local";
        await FillAndSubmitAsync(page, username, email, "ValidPass123", "ValidPass123");

        if (shouldSucceed)
        {
            await page.WaitForURLAsync(u => u.Contains("/puzzles"), new() { Timeout = 10_000 });
            page.Url.Should().Contain("/puzzles");
        }
        else
        {
            // Auf Server-Re-Render warten — bei invalidem Model bleibt die URL /register.
            await page.WaitForSelectorAsync(".form-error", new() { Timeout = 5_000 });
            page.Url.Should().Contain("/register");
            var errors = await page.Locator(".form-error").AllInnerTextsAsync();
            // Validation-Message enthält "3–50" (Bindestrich-Variante in der UI).
            errors.Should().NotBeEmpty();
        }
    }

    // T013 — Duplicate Username → Identity meldet Fehler.
    [Fact]
    public async Task T013_DuplicateUsername_ShowsError()
    {
        var username = UniqueUser("dup");
        const string password = "ValidPass123";

        // erste Registrierung — sollte succeeden
        await using (var ctx1 = await _fx.NewContextAsync())
        {
            var page1 = await ctx1.NewPageAsync();
            await FillAndSubmitAsync(page1, username, $"{username}@e2e.local", password, password);
            await page1.WaitForURLAsync(u => u.Contains("/puzzles"), new() { Timeout = 10_000 });
        }

        // zweite mit gleichem Username — andere Email — muss fehlschlagen
        await using var ctx2 = await _fx.NewContextAsync();
        var page2 = await ctx2.NewPageAsync();
        await FillAndSubmitAsync(page2, username, $"{username}2@e2e.local", password, password);

        page2.Url.Should().Contain("/register");
        var banner = await page2.GetByTestId("register-error-banner").InnerTextAsync();
        banner.Should().Contain(username);
    }

    // T014 — Negative: Ungültiges Email-Format.
    //   Wir verwenden "x@y" — passiert den HTML5-`type=email`-Validator (hat @),
    //   wird aber von unserer Server-Side Regex `[^\s@]+@[^\s@]+\.[^\s@]+` abgelehnt
    //   (fehlt TLD-Punkt).
    [Fact]
    public async Task T014_InvalidEmailFormat_ShowsValidationError()
    {
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();
        await FillAndSubmitAsync(page, UniqueUser("eml"), "test@nodot", "ValidPass123", "ValidPass123");

        await page.WaitForSelectorAsync(".form-error", new() { Timeout = 5_000 });
        page.Url.Should().Contain("/register");
        var errors = await page.Locator(".form-error").AllInnerTextsAsync();
        errors.Should().NotBeEmpty();
        errors.Any(e => e.Contains("Email") || e.Contains("E-Mail") || e.Contains("gültige")).Should().BeTrue();
    }

    // T015 — Negative: Passwort-Confirm-Mismatch.
    [Fact]
    public async Task T015_PasswordConfirmMismatch_ShowsValidationError()
    {
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();
        var u = UniqueUser("mm");
        await FillAndSubmitAsync(page, u, $"{u}@e2e.local", "ValidPass123", "OtherPass123");

        await page.WaitForSelectorAsync(".form-error", new() { Timeout = 5_000 });
        page.Url.Should().Contain("/register");
        var errors = await page.Locator(".form-error").AllInnerTextsAsync();
        errors.Any(e => e.Contains("nicht überein") || e.Contains("stimmen nicht")).Should().BeTrue();
    }

    // T016 — Negative: Passwort < 8 Zeichen.
    [Fact]
    public async Task T016_PasswordTooShort_ShowsValidationError()
    {
        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();
        var u = UniqueUser("pw");
        await FillAndSubmitAsync(page, u, $"{u}@e2e.local", "Sh0rt", "Sh0rt");

        await page.WaitForSelectorAsync(".form-error", new() { Timeout = 5_000 });
        page.Url.Should().Contain("/register");
        var errors = await page.Locator(".form-error").AllInnerTextsAsync();
        errors.Any(e => e.Contains("8 Zeichen") || e.Contains("mindestens 8")).Should().BeTrue();
    }

    // T017 — Security: Passwort wird NIE als Klartext in der DB gespeichert.
    [Fact]
    public async Task T017_PasswordIsHashed_NeverStoredInPlainText()
    {
        var username = UniqueUser("sec");
        const string password = "PlainText1234";

        await using var ctx = await _fx.NewContextAsync();
        var page = await ctx.NewPageAsync();
        await FillAndSubmitAsync(page, username, $"{username}@e2e.local", password, password);
        await page.WaitForURLAsync(u => u.Contains("/puzzles"), new() { Timeout = 10_000 });

        await using var db = _fx.NewDbContext();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.UserName == username);
        user.PasswordHash.Should().NotBeNull();
        user.PasswordHash!.Should().NotContain(password);
        // ASP.NET Identity PasswordHash startet mit Version-Byte "AQAAAA..." in Base64.
        user.PasswordHash.Length.Should().BeGreaterThan(40);
    }
}
