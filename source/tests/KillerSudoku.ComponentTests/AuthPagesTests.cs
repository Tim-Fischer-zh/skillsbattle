using Bunit;
using FluentAssertions;
using KillerSudoku.Data.Entities;
using KillerSudoku.Web.Components.Pages.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Security.Claims;

namespace KillerSudoku.ComponentTests;

/// <summary>
/// T018/T019/T020 — bUnit tests for the Register + Login pages.
/// Spec anchor: docs/use-cases.md UC02 (Register), UC03 (Login),
/// docs/validation.md V01 (Username), V02 (Email), V03 (Password), V04 (Rate-Limit).
///
/// Login fail-message constant is strictly "Username oder Passwort falsch"
/// per AC03.1 — no disclosure of which field was wrong.
/// </summary>
public class AuthPagesTests : BunitContext
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;

    public AuthPagesTests()
    {
        _userManager = AuthTestHelpers.CreateUserManager();
        _signInManager = AuthTestHelpers.CreateSignInManager(_userManager);

        Services.AddSingleton(_userManager);
        Services.AddSingleton(_signInManager);

        // MainLayout uses AuthorizeView, which requires an AuthenticationStateProvider.
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider, AnonymousAuthStateProvider>();
        Services.AddCascadingAuthenticationState();
    }

    // ----- Test 1: Register page renders 4 form fields + submit -----

    [Fact]
    public void Register_RendersFourFormFields_AndPrimarySubmit()
    {
        var cut = Render<Register>();

        // 4 inputs: Username, Email, Password, PasswordConfirm
        cut.Find("[data-testid=register-username]").Should().NotBeNull();
        cut.Find("[data-testid=register-email]").Should().NotBeNull();
        cut.Find("[data-testid=register-password]").Should().NotBeNull();
        cut.Find("[data-testid=register-password-confirm]").Should().NotBeNull();

        // Submit button labelled "Account erstellen"
        var submit = cut.Find("[data-testid=register-submit]");
        submit.TextContent.Trim().Should().Be("Account erstellen");

        // Link to /login
        cut.Markup.Should().Contain("/login");
    }

    [Fact]
    public void Register_RendersValidationHelpers_FromMockupBriefS2()
    {
        var cut = Render<Register>();

        // S2 brief: "3–50 Zeichen, A–Z 0–9 _ -"
        cut.Markup.Should().Contain("3").And.Contain("50");
        cut.Markup.Should().Contain("Buchstaben").And.Contain("Zahlen");

        // H2 title from S2 brief
        cut.Find("h2").TextContent.Should().Contain("Account erstellen");
    }

    // ----- Test 2: Login page renders 2 fields + submit + link -----

    [Fact]
    public void Login_RendersTwoFormFields_SubmitButton_AndRegisterLink()
    {
        var cut = Render<Login>();

        cut.Find("[data-testid=login-username]").Should().NotBeNull();
        cut.Find("[data-testid=login-password]").Should().NotBeNull();

        var submit = cut.Find("[data-testid=login-submit]");
        submit.TextContent.Trim().Should().Be("Anmelden");

        cut.Markup.Should().Contain("/register");
        cut.Find("h2").TextContent.Should().Contain("Anmelden");
    }

    // ----- Test 3 (T019/T020): generic error on failed login -----

    [Fact]
    public async Task Login_OnFailedSignIn_DisplaysGenericErrorMessage()
    {
        // Arrange — SignInManager returns Failed for any credentials.
        _signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>())
            .Returns(Task.FromResult(SignInResult.Failed));

        var cut = Render<Login>();

        // Act — fill in credentials and submit the form.
        cut.Find("[data-testid=login-username]").Change("alice");
        cut.Find("[data-testid=login-password]").Change("wrongpass1");
        await cut.Find("[data-testid=login-form]").SubmitAsync();

        // Assert — V03 + AC03.1: the message is exactly the generic constant.
        var banner = cut.Find("[data-testid=login-error-banner]");
        banner.TextContent.Should().Contain("Username oder Passwort falsch",
            "AC03.1 / V03 require a generic error that hides which field was wrong");

        // It must NOT expose which field was wrong — the message is "Username oder Passwort falsch"
        // (generic disjunction), nothing more specific.
        banner.TextContent.Should().NotContainAny(
            "Username ist falsch", "Username nicht gefunden", "Email nicht gefunden",
            "ungültiger Benutzername", "ungültiges Passwort");
    }

    [Fact]
    public async Task Login_OnLockout_DisplaysRateLimitMessage()
    {
        // Arrange — V04 rate-limit kicks in.
        _signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>())
            .Returns(Task.FromResult(SignInResult.LockedOut));

        var cut = Render<Login>();

        // Act
        cut.Find("[data-testid=login-username]").Change("alice");
        cut.Find("[data-testid=login-password]").Change("whatever1");
        await cut.Find("[data-testid=login-form]").SubmitAsync();

        // Assert — V04 wording per validation.md
        var banner = cut.Find("[data-testid=login-error-banner]");
        banner.TextContent.Should().Contain("Zu viele");
        banner.TextContent.Should().Contain("5");
    }

    private sealed class AnonymousAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
