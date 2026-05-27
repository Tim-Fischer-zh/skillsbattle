using FluentAssertions;
using KillerSudoku.Core.Models;
using KillerSudoku.Data.Entities;
using KillerSudoku.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.IntegrationTests;

/// <summary>
/// GameService Integration-Tests (UC6 Solve, UC9 Check, UC10 Complete, UC13 Pause).
/// </summary>
[Collection(MsSqlCollection.Name)]
public sealed class GameServiceTests
{
    private readonly MsSqlContainerFixture _fx;
    public GameServiceTests(MsSqlContainerFixture fx) => _fx = fx;

    private async Task<(int userId, int puzzleId, PuzzleInputDto input)> CreateSavedPuzzleAsync(
        int seed, byte difficulty = 1)
    {
        // Default difficulty=1 → schnelle Generierung. Tests, die wirklich ein
        // leeres Grid brauchen (T050), übergeben difficulty=3 oder leeren Cells
        // explizit nach StartGameAsync.
        var userId = await ServiceFactory.CreateUserAsync(_fx);
        var input = ServiceFactory.Generator().Generate(difficulty, new Random(seed));
        var svc = ServiceFactory.NewPuzzleService(_fx);
        var saved = await svc.SaveIfSolvableAsync(input, userId);
        saved.Status.Should().Be(SaveStatus.Saved);
        return (userId, saved.PuzzleId!.Value, input);
    }

    /// <summary>
    /// Räumt alle Prefill-Clues aus dem Game ab — für Tests die mit garantiert
    /// leeren GameCells starten müssen.
    /// </summary>
    private async Task ClearAllCellsAsync(int gameId)
    {
        await using var ctx = ServiceFactory.NewContext(_fx);
        var cells = await ctx.GameCells.Where(c => c.GameId == gameId).ToListAsync();
        foreach (var c in cells) c.CellValue = null;
        await ctx.SaveChangesAsync();
    }

    // -------------------------------------------------------------------
    // T050 — StartGameAsync legt Game-Row + 81 GameCells an.
    //   Verwendet Difficulty 3 (keine Prefill-Clues) damit alle 81 Zellen leer sind.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T050_StartGame_CreatesGameAndAll81Cells()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1050);

        var svc = ServiceFactory.NewGameService(_fx);
        var gameId = await svc.StartGameAsync(userId, puzzleId);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var game = await ctx.Games.Include(g => g.Cells).FirstAsync(g => g.Id == gameId);
        game.UserId.Should().Be(userId);
        game.PuzzleId.Should().Be(puzzleId);
        game.IsCompleted.Should().BeFalse();
        // 81 GameCells müssen erzeugt werden — Werte können je nach Difficulty
        // teilweise als Prefill-Clues vorbelegt sein (UC06).
        game.Cells.Should().HaveCount(81);
    }

    // -------------------------------------------------------------------
    // Prefill-Verhalten: Difficulty 1 → 20 vorgefüllte Zellen, deterministisch pro Puzzle.
    // -------------------------------------------------------------------
    [Fact]
    public async Task StartGame_Difficulty1_Prefills20Cells()
    {
        var (userId, puzzleId, input) = await CreateSavedPuzzleAsync(seed: 5001, difficulty: 1);

        var gameId = await ServiceFactory.NewGameService(_fx).StartGameAsync(userId, puzzleId);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var filled = await ctx.GameCells
            .Where(c => c.GameId == gameId && c.CellValue != null)
            .ToListAsync();
        filled.Should().HaveCount(20);

        // Werte stimmen mit der eindeutigen Solver-Lösung überein
        var solution = ServiceFactory.Solver().Solve(new byte[9, 9], input.Cages).Solution!;
        foreach (var cell in filled)
            cell.CellValue.Should().Be(solution[cell.RowIdx, cell.ColIdx]);
    }

    [Fact]
    public async Task StartGame_Difficulty1_PrefillIsDeterministicPerPuzzle()
    {
        var (user1, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 5002, difficulty: 1);
        var user2 = await ServiceFactory.CreateUserAsync(_fx);

        var g1 = await ServiceFactory.NewGameService(_fx).StartGameAsync(user1, puzzleId);
        var g2 = await ServiceFactory.NewGameService(_fx).StartGameAsync(user2, puzzleId);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var cells1 = await ctx.GameCells
            .Where(c => c.GameId == g1 && c.CellValue != null)
            .Select(c => new { c.RowIdx, c.ColIdx, c.CellValue })
            .OrderBy(x => x.RowIdx).ThenBy(x => x.ColIdx).ToListAsync();
        var cells2 = await ctx.GameCells
            .Where(c => c.GameId == g2 && c.CellValue != null)
            .Select(c => new { c.RowIdx, c.ColIdx, c.CellValue })
            .OrderBy(x => x.RowIdx).ThenBy(x => x.ColIdx).ToListAsync();

        cells1.Should().Equal(cells2,
            "Spieler auf demselben Puzzle bekommen identische Startbelegung (Fairness).");
    }

    [Fact]
    public async Task StartGame_Difficulty3_HasNoPrefill()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 5003, difficulty: 3);
        var gameId = await ServiceFactory.NewGameService(_fx).StartGameAsync(userId, puzzleId);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var filledCount = await ctx.GameCells.CountAsync(c => c.GameId == gameId && c.CellValue != null);
        filledCount.Should().Be(0);
    }

    // -------------------------------------------------------------------
    // T051 — Zweiter StartGame für gleichen User+Puzzle gibt bestehendes Game zurück
    //   (filtered unique index UX_Game_ActiveOnly + Service-Logik).
    // -------------------------------------------------------------------
    [Fact]
    public async Task T051_StartGame_TwiceForSamePuzzle_ReturnsSameId()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1051);

        var first = await ServiceFactory.NewGameService(_fx).StartGameAsync(userId, puzzleId);
        var second = await ServiceFactory.NewGameService(_fx).StartGameAsync(userId, puzzleId);

        second.Should().Be(first);
    }

    // -------------------------------------------------------------------
    // T080 — CheckSolutionAsync mit korrekter Lösung → IsCorrect=true.
    //   Wir füllen das Game mit der vom Generator gelieferten Lösung.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T080_CheckSolution_CorrectComplete_ReturnsIsCorrect()
    {
        var (userId, puzzleId, input) = await CreateSavedPuzzleAsync(seed: 1080);

        var gameSvc = ServiceFactory.NewGameService(_fx);
        var gameId = await gameSvc.StartGameAsync(userId, puzzleId);

        // Hole die Solver-Lösung (Generator hat verifiziert dass sie eindeutig ist)
        var solveResult = ServiceFactory.Solver().Solve(new byte[9, 9], input.Cages);
        solveResult.Solutions.Should().Be(1);
        var solution = solveResult.Solution!;

        // Trage die Lösung in das Game-Grid ein
        for (byte r = 0; r < 9; r++)
        for (byte c = 0; c < 9; c++)
            await gameSvc.SetCellValueAsync(gameId, r, c, solution[r, c]);

        var check = await ServiceFactory.NewGameService(_fx).CheckSolutionAsync(gameId);
        check.IsCorrect.Should().BeTrue();
        check.FailReason.Should().BeNull();
    }

    // -------------------------------------------------------------------
    // T081 — Incomplete Grid → IsCorrect=false, Reason=Incomplete.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T081_CheckSolution_Incomplete_ReturnsIncomplete()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1081);
        var gameSvc = ServiceFactory.NewGameService(_fx);
        var gameId = await gameSvc.StartGameAsync(userId, puzzleId);

        // Nur eine Zelle befüllen, Rest leer.
        await gameSvc.SetCellValueAsync(gameId, 0, 0, 5);

        var check = await ServiceFactory.NewGameService(_fx).CheckSolutionAsync(gameId);
        check.IsCorrect.Should().BeFalse();
        check.FailReason.Should().Be(CheckFailReason.Incomplete);
    }

    // -------------------------------------------------------------------
    // T090 — CompleteGameAsync setzt EndTime + TimeSeconds + Score, IsCompleted=true.
    //   Score-Formel: max(0, 10000 - time - hints*300).
    // -------------------------------------------------------------------
    [Fact]
    public async Task T090_CompleteGame_PersistsTimeAndScore()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1090);
        var startWall = new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero);
        var endWall   = startWall.AddSeconds(300); // 5 Minuten Spielzeit

        var fakeClock = new FakeTimeProvider(startWall);
        var startSvc = ServiceFactory.NewGameService(_fx, fakeClock);
        var gameId = await startSvc.StartGameAsync(userId, puzzleId);

        fakeClock.Now = endWall;
        var completeSvc = ServiceFactory.NewGameService(_fx, fakeClock);
        var score = await completeSvc.CompleteGameAsync(gameId);

        // Erwartet: 10000 - 300 - 0*300 = 9700
        score.Should().Be(9700);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var game = await ctx.Games.FirstAsync(g => g.Id == gameId);
        game.IsCompleted.Should().BeTrue();
        game.TimeSeconds.Should().Be(300);
        game.Score.Should().Be(9700);
        game.EndTime.Should().NotBeNull();
    }

    // -------------------------------------------------------------------
    // T120 — Pause/Resume akkumuliert TotalPausedSeconds; CompleteGame
    //   zählt Pausenzeit nicht zur Spielzeit.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T120_PauseResume_ExcludesPausedTimeFromScore()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1120);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 27, 11, 0, 0, TimeSpan.Zero));

        var startSvc = ServiceFactory.NewGameService(_fx, clock);
        var gameId = await startSvc.StartGameAsync(userId, puzzleId);

        clock.Now = clock.Now.AddSeconds(100);            // 100s gespielt
        await ServiceFactory.NewGameService(_fx, clock).PauseAsync(gameId);

        clock.Now = clock.Now.AddSeconds(60);             // 60s pausiert
        await ServiceFactory.NewGameService(_fx, clock).ResumeAsync(gameId);

        clock.Now = clock.Now.AddSeconds(200);            // 200s mehr gespielt
        var score = await ServiceFactory.NewGameService(_fx, clock).CompleteGameAsync(gameId);

        // Erwartete TimeSeconds = 100 + 200 = 300 (Pause exkludiert).
        // Score = 10000 - 300 - 0 = 9700.
        score.Should().Be(9700);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var game = await ctx.Games.FirstAsync(g => g.Id == gameId);
        game.TimeSeconds.Should().Be(300);
        game.TotalPausedSeconds.Should().Be(60);
    }

    // -------------------------------------------------------------------
    // T052 — Boundary: SetCellValueAsync mit Wert 1, 9 OK; 10 / 0 reject.
    //   UC06 AC06.2 + DB CHECK CellValue BETWEEN 1 AND 9.
    // -------------------------------------------------------------------
    [Theory]
    [InlineData((byte)1, true)]
    [InlineData((byte)9, true)]
    [InlineData((byte)10, false)]
    [InlineData((byte)0, false)]
    public async Task T052_SetCellValue_BoundaryValues(byte value, bool shouldSucceed)
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1500 + value);
        var svc = ServiceFactory.NewGameService(_fx);
        var gameId = await svc.StartGameAsync(userId, puzzleId);

        if (shouldSucceed)
        {
            await svc.SetCellValueAsync(gameId, 0, 0, value);
            await using var ctx = ServiceFactory.NewContext(_fx);
            var cell = await ctx.GameCells.FirstAsync(c => c.GameId == gameId && c.RowIdx == 0 && c.ColIdx == 0);
            cell.CellValue.Should().Be(value);
        }
        else
        {
            var act = async () => await svc.SetCellValueAsync(gameId, 0, 0, value);
            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }
    }

    // -------------------------------------------------------------------
    // T053 — Eingabe einer Zahl persistiert über das Service-Interface (zugleich
    //   Vertragstest für die UI-Tastatur). Spec §UC06.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T053_SetCellValue_PersistsValueViaService()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1530);
        var svc = ServiceFactory.NewGameService(_fx);
        var gameId = await svc.StartGameAsync(userId, puzzleId);

        await svc.SetCellValueAsync(gameId, 3, 5, 7);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var cell = await ctx.GameCells.FirstAsync(c => c.GameId == gameId && c.RowIdx == 3 && c.ColIdx == 5);
        cell.CellValue.Should().Be(7);
    }

    // -------------------------------------------------------------------
    // T091 — TimeSeconds = (EndTime - StartTime) - TotalPausedSeconds.
    //   Variation von T120 mit konkreten Werten + expliziter Time-Assertion.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T091_CompleteGame_TimeSecondsExcludesPause()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1591);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero));

        var gameId = await ServiceFactory.NewGameService(_fx, clock).StartGameAsync(userId, puzzleId);
        clock.Now = clock.Now.AddSeconds(200);
        await ServiceFactory.NewGameService(_fx, clock).PauseAsync(gameId);
        clock.Now = clock.Now.AddSeconds(120);
        await ServiceFactory.NewGameService(_fx, clock).ResumeAsync(gameId);
        clock.Now = clock.Now.AddSeconds(280);
        await ServiceFactory.NewGameService(_fx, clock).CompleteGameAsync(gameId);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var g = await ctx.Games.FirstAsync(x => x.Id == gameId);
        // Spielzeit = 200 + 280 = 480 s; Pause = 120 s wird ausgeschlossen.
        g.TimeSeconds.Should().Be(480);
        g.TotalPausedSeconds.Should().Be(120);
    }

    // -------------------------------------------------------------------
    // T092 — Falsche Lösung lässt IsCompleted=0 (kein Auto-Complete via Check).
    //   CheckSolutionAsync mutiert das Game NICHT — IsCompleted bleibt unverändert.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T092_CheckSolution_WithWrongAnswer_LeavesIsCompletedFalse()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1592);
        var svc = ServiceFactory.NewGameService(_fx);
        var gameId = await svc.StartGameAsync(userId, puzzleId);

        // Falsch: alle Zellen auf 1
        for (byte r = 0; r < 9; r++)
            for (byte c = 0; c < 9; c++)
                await svc.SetCellValueAsync(gameId, r, c, 1);

        var check = await ServiceFactory.NewGameService(_fx).CheckSolutionAsync(gameId);

        check.IsCorrect.Should().BeFalse();
        await using var ctx = ServiceFactory.NewContext(_fx);
        var game = await ctx.Games.FirstAsync(g => g.Id == gameId);
        game.IsCompleted.Should().BeFalse();
        game.Score.Should().BeNull();
    }

    // -------------------------------------------------------------------
    // T093 — DB-Constraint: Negative TimeSeconds wird vom CHECK abgelehnt.
    //   (Semantischer Duplikat zu DbConstraintTests T087 — explizit als T093 markiert.)
    // -------------------------------------------------------------------
    [Fact]
    public async Task T093_GameTimeSeconds_Negative_RejectedByDbConstraint()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1593);

        await using var ctx = ServiceFactory.NewContext(_fx);
        ctx.Games.Add(new Game
        {
            UserId = userId,
            PuzzleId = puzzleId,
            StartTime = DateTime.UtcNow,
            TimeSeconds = -1,
        });
        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // -------------------------------------------------------------------
    // T121 — Resume erhöht TotalPausedSeconds um die Pausen-Dauer.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T121_Resume_IncrementsTotalPausedSeconds()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1621);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 27, 13, 0, 0, TimeSpan.Zero));

        var gameId = await ServiceFactory.NewGameService(_fx, clock).StartGameAsync(userId, puzzleId);
        await ServiceFactory.NewGameService(_fx, clock).PauseAsync(gameId);
        clock.Now = clock.Now.AddSeconds(45);
        await ServiceFactory.NewGameService(_fx, clock).ResumeAsync(gameId);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var g = await ctx.Games.FirstAsync(x => x.Id == gameId);
        g.TotalPausedSeconds.Should().BeInRange(44, 46);
        g.IsPaused.Should().BeFalse();
        g.PausedAt.Should().BeNull();
    }

    // -------------------------------------------------------------------
    // T122 — Pro (User, Puzzle) max. 1 aktives Game — zweiter StartGame
    //   gibt die existierende GameId zurück (kein Duplicate-Insert).
    //   (Semantisches Duplikat zu T051 — explizit als T122 für UC13.)
    // -------------------------------------------------------------------
    [Fact]
    public async Task T122_StartGame_OnlyOneActivePerUserPuzzle()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1622);

        var first = await ServiceFactory.NewGameService(_fx).StartGameAsync(userId, puzzleId);
        var second = await ServiceFactory.NewGameService(_fx).StartGameAsync(userId, puzzleId);
        second.Should().Be(first);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var activeCount = await ctx.Games.CountAsync(g =>
            g.UserId == userId && g.PuzzleId == puzzleId && !g.IsCompleted);
        activeCount.Should().Be(1);
    }

    // -------------------------------------------------------------------
    // T123 — Resume erhält alle vor der Pause gesetzten GameCells unverändert.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T123_Resume_PreservesAllGameCells()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1623);
        var svc = ServiceFactory.NewGameService(_fx);
        var gameId = await svc.StartGameAsync(userId, puzzleId);

        // 5 Zellen setzen
        var seeded = new[] { (0,0,(byte)5), (1,2,(byte)3), (4,4,(byte)7), (6,1,(byte)1), (8,8,(byte)9) };
        foreach (var (r, c, v) in seeded)
            await svc.SetCellValueAsync(gameId, (byte)r, (byte)c, v);

        await svc.PauseAsync(gameId);
        await svc.ResumeAsync(gameId);

        await using var ctx = ServiceFactory.NewContext(_fx);
        foreach (var (r, c, v) in seeded)
        {
            var cell = await ctx.GameCells.FirstAsync(g => g.GameId == gameId && g.RowIdx == r && g.ColIdx == c);
            cell.CellValue.Should().Be(v);
        }
    }

    // -------------------------------------------------------------------
    // T125 — Mehrere Pause/Resume-Cycles akkumulieren TotalPausedSeconds.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T125_MultiplePauseResume_AccumulatesPausedSeconds()
    {
        var (userId, puzzleId, _) = await CreateSavedPuzzleAsync(seed: 1625);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 27, 14, 0, 0, TimeSpan.Zero));

        var gameId = await ServiceFactory.NewGameService(_fx, clock).StartGameAsync(userId, puzzleId);

        // 3 Cycles à 10 Sekunden Pause
        for (int i = 0; i < 3; i++)
        {
            await ServiceFactory.NewGameService(_fx, clock).PauseAsync(gameId);
            clock.Now = clock.Now.AddSeconds(10);
            await ServiceFactory.NewGameService(_fx, clock).ResumeAsync(gameId);
            clock.Now = clock.Now.AddSeconds(5); // 5 s gespielt zwischen den Pausen
        }

        await using var ctx = ServiceFactory.NewContext(_fx);
        var g = await ctx.Games.FirstAsync(x => x.Id == gameId);
        g.TotalPausedSeconds.Should().BeInRange(29, 31); // ~30 s ± 1
    }
}

/// <summary>
/// Fake clock for time-based tests — increments via assignment to <see cref="Now"/>.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; }
    public FakeTimeProvider(DateTimeOffset start) => Now = start;
    public override DateTimeOffset GetUtcNow() => Now;
}
