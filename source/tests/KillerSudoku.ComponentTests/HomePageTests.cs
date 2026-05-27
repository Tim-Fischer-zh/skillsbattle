using Bunit;
using FluentAssertions;
using KillerSudoku.Web.Components.Pages;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace KillerSudoku.ComponentTests;

/// <summary>
/// T002 — Verify that the Home page contains the four README §1 rule strings
/// verbatim. Spec anchor: docs/use-cases.md UC01 AC01.3 + docs/test-protocol.md T002.
/// </summary>
public class HomePageTests : BunitContext
{
    public HomePageTests()
    {
        // AuthorizeView in MainLayout (and reused indirectly) needs an AuthorizationStateProvider.
        // We use an anonymous (unauthenticated) state since UC01 is public.
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider, AnonymousAuthStateProvider>();
        Services.AddCascadingAuthenticationState();
    }

    [Fact]
    public void Home_RendersAllFourRulesFromReadmeSection1()
    {
        // Arrange + Act
        var cut = Render<Home>();
        var markup = cut.Markup;

        // Assert — all four rules from README §1 (lines 13–16) are present verbatim.
        markup.Should().Contain("Each row, column, and nonet contains each number exactly once",
            "rule 1 (Sudoku constraint) must be visible per AC01.3");
        markup.Should().Contain("The sum of all numbers in a cage must match",
            "rule 2 (Killer cage-sum) must be visible per AC01.3");
        markup.Should().Contain("No number appears more than once in a cage",
            "rule 3 (Killer no-duplicate-in-cage) must be visible per AC01.3");
        markup.Should().Contain("The solution must be unique",
            "rule 4 (uniqueness) must be visible per AC01.3");

        // Spot-check the fragments the test protocol calls out explicitly.
        markup.Should().Contain("exactly once");
        markup.Should().Contain("must match");
        markup.Should().Contain("more than once");
        markup.Should().Contain("must be unique");
    }

    [Fact]
    public void Home_RendersHeroH1_AndCallToActions()
    {
        var cut = Render<Home>();

        cut.Find("h1").TextContent.Should().Contain("Killer Sudoku");
        cut.FindAll("a.btn").Should().NotBeEmpty();
        cut.Markup.Should().Contain("/register");
        cut.Markup.Should().Contain("/login");
    }

    private sealed class AnonymousAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
