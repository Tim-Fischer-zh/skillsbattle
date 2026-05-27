using KillerSudoku.Core.Abstractions;
using KillerSudoku.Core.Services;
using KillerSudoku.Data.Entities;
using KillerSudoku.Data.Persistence;
using KillerSudoku.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.IntegrationTests.Fixtures;

/// <summary>
/// Helpers für Service-Integration-Tests: erzeugt frischen DbContext, baut die
/// Application-Services manuell (wie der DI-Container im Web-Projekt) und
/// stellt einen deterministischen <see cref="PuzzleGenerator"/> mit Seed-RNG
/// für reproduzierbare Tests bereit.
/// </summary>
internal static class ServiceFactory
{
    public static SudokuDbContext NewContext(MsSqlContainerFixture fx)
    {
        var options = new DbContextOptionsBuilder<SudokuDbContext>()
            .UseSqlServer(fx.ConnectionString)
            .Options;
        return new SudokuDbContext(options);
    }

    public static ISolverService Solver() => new SolverService();
    public static SolutionValidator Validator() => new SolutionValidator();
    public static PuzzleStructureValidator StructValidator() => new PuzzleStructureValidator();
    public static IScoreCalculator Score() => new ScoreCalculator();
    public static IPuzzleGenerator Generator() => new PuzzleGenerator(Solver());

    public static PuzzleService NewPuzzleService(MsSqlContainerFixture fx)
        => new(NewContext(fx), StructValidator(), Solver());

    public static GameService NewGameService(MsSqlContainerFixture fx, TimeProvider? clock = null)
        => new(NewContext(fx), Validator(), Score(), clock);

    public static HintService NewHintService(MsSqlContainerFixture fx)
        => new(NewContext(fx), Solver());

    public static HighscoreService NewHighscoreService(MsSqlContainerFixture fx)
        => new(NewContext(fx));

    /// <summary>
    /// Creates a minimal user (no Identity, just bare AppUser row) and returns its Id.
    /// Test-isolation via guid suffix in username + email.
    /// </summary>
    public static async Task<int> CreateUserAsync(MsSqlContainerFixture fx, string? suffix = null)
    {
        suffix ??= Guid.NewGuid().ToString("N")[..8];
        await using var ctx = NewContext(fx);
        var user = new AppUser
        {
            UserName = $"u_{suffix}",
            NormalizedUserName = $"U_{suffix}".ToUpperInvariant(),
            Email = $"u_{suffix}@test.ch",
            NormalizedEmail = $"U_{suffix}@TEST.CH".ToUpperInvariant(),
            EmailConfirmed = true,
            PasswordHash = "fake-hash",
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }
}
