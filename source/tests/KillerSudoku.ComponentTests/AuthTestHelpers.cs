using KillerSudoku.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace KillerSudoku.ComponentTests;

/// <summary>
/// Helpers to construct NSubstitute mocks of UserManager&lt;AppUser&gt; and SignInManager&lt;AppUser&gt;.
/// Their constructors require ~7 collaborators each, so this keeps the test files focused.
/// </summary>
internal static class AuthTestHelpers
{
    public static UserManager<AppUser> CreateUserManager()
    {
        var store = Substitute.For<IUserStore<AppUser>>();
        return Substitute.For<UserManager<AppUser>>(
            store,
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<AppUser>>(),
            Array.Empty<IUserValidator<AppUser>>(),
            Array.Empty<IPasswordValidator<AppUser>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<AppUser>>>());
    }

    public static SignInManager<AppUser> CreateSignInManager(UserManager<AppUser> userManager)
    {
        var contextAccessor = Substitute.For<IHttpContextAccessor>();
        contextAccessor.HttpContext.Returns(new DefaultHttpContext());

        return Substitute.For<SignInManager<AppUser>>(
            userManager,
            contextAccessor,
            Substitute.For<IUserClaimsPrincipalFactory<AppUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<ILogger<SignInManager<AppUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<AppUser>>());
    }
}
