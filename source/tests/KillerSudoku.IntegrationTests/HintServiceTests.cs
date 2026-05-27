using FluentAssertions;
using KillerSudoku.Core.Models;
using KillerSudoku.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.IntegrationTests;

/// <summary>
/// HintService Integration-Tests (UC7 — Hint provides a valid suggestion,
/// counts toward HintsUsed, and is audited in HintLog).
/// </summary>
[Collection(MsSqlCollection.Name)]
public sealed class HintServiceTests
{
    private readonly MsSqlContainerFixture _fx;
    public HintServiceTests(MsSqlContainerFixture fx) => _fx = fx;

    private async Task<(int gameId, byte[,] solution)> StartFreshGameAsync(int seed)
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);
        var input = ServiceFactory.Generator().Generate(difficulty: 1, new Random(seed));
        var saved = await ServiceFactory.NewPuzzleService(_fx).SaveIfSolvableAsync(input, userId);
        saved.Status.Should().Be(SaveStatus.Saved);

        var gameId = await ServiceFactory.NewGameService(_fx).StartGameAsync(userId, saved.PuzzleId!.Value);
        var solution = ServiceFactory.Solver().Solve(new byte[9, 9], input.Cages).Solution!;
        return (gameId, solution);
    }

    // -------------------------------------------------------------------
    // T060 — Hint liefert einen gültigen (Row, Col, Value) aus 1..9.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T060_GetHint_ReturnsValidPlacement()
    {
        var (gameId, solution) = await StartFreshGameAsync(seed: 2060);

        var hint = await ServiceFactory.NewHintService(_fx).GetHintAsync(gameId);

        hint.Row.Should().BeInRange((byte)0, (byte)8);
        hint.Col.Should().BeInRange((byte)0, (byte)8);
        hint.Value.Should().BeInRange((byte)1, (byte)9);
        // Hint muss zur korrekten eindeutigen Lösung passen
        hint.Value.Should().Be(solution[hint.Row, hint.Col]);
    }

    // -------------------------------------------------------------------
    // T061 — Hint inkrementiert Game.HintsUsed (UC07/UC08 Score-Coupling).
    // -------------------------------------------------------------------
    [Fact]
    public async Task T061_GetHint_IncrementsHintsUsed()
    {
        var (gameId, _) = await StartFreshGameAsync(seed: 2061);

        await ServiceFactory.NewHintService(_fx).GetHintAsync(gameId);
        await ServiceFactory.NewHintService(_fx).GetHintAsync(gameId);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var game = await ctx.Games.FirstAsync(g => g.Id == gameId);
        game.HintsUsed.Should().Be(2);
    }

    // -------------------------------------------------------------------
    // T062 — Hint schreibt einen HintLog-Eintrag pro Aufruf.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T062_GetHint_WritesHintLogEntry()
    {
        var (gameId, _) = await StartFreshGameAsync(seed: 2062);

        var hint = await ServiceFactory.NewHintService(_fx).GetHintAsync(gameId);

        await using var ctx = ServiceFactory.NewContext(_fx);
        var logs = await ctx.HintLogs.Where(h => h.GameId == gameId).ToListAsync();
        logs.Should().HaveCount(1);
        logs[0].RowIdx.Should().Be(hint.Row);
        logs[0].ColIdx.Should().Be(hint.Col);
    }

    // -------------------------------------------------------------------
    // T063 — Hint auf vollständig befülltem Grid wird abgelehnt (V11).
    // -------------------------------------------------------------------
    [Fact]
    public async Task T063_GetHint_OnCompleteGrid_Throws()
    {
        var (gameId, solution) = await StartFreshGameAsync(seed: 2063);

        var gameSvc = ServiceFactory.NewGameService(_fx);
        for (byte r = 0; r < 9; r++)
        for (byte c = 0; c < 9; c++)
            await gameSvc.SetCellValueAsync(gameId, r, c, solution[r, c]);

        var act = async () => await ServiceFactory.NewHintService(_fx).GetHintAsync(gameId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*vollständig*");
    }

    // -------------------------------------------------------------------
    // T064 — NakedSingle-Strategy wird gewählt wenn eine Zelle nur 1 Kandidat hat.
    //   Setup: fülle Reihe 0 mit Werten 1..8 (in der Lösungs-Reihenfolge), Zelle (0,8)
    //   bleibt frei und hat damit naked-single = letzter Wert.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T064_GetHint_ReturnsNakedSingleStrategy()
    {
        var (gameId, solution) = await StartFreshGameAsync(seed: 2064);

        var gameSvc = ServiceFactory.NewGameService(_fx);
        // Fülle alle Zellen außer (0,8) mit der Lösung — (0,8) hat damit
        // nur 1 möglichen Wert (NakedSingle).
        for (byte r = 0; r < 9; r++)
        for (byte c = 0; c < 9; c++)
        {
            if (r == 0 && c == 8) continue;
            await gameSvc.SetCellValueAsync(gameId, r, c, solution[r, c]);
        }

        var hint = await ServiceFactory.NewHintService(_fx).GetHintAsync(gameId);

        hint.Strategy.Should().Be(HintStrategy.NakedSingle);
        hint.Row.Should().Be((byte)0);
        hint.Col.Should().Be((byte)8);
        hint.Value.Should().Be(solution[0, 8]);
    }

    // -------------------------------------------------------------------
    // T065 — CageForced-Strategy: eine Cage mit nur 1 unbefüllten Zelle wird
    //   durch die Sum-Differenz erzwungen.
    //   Solution wird befüllt — bis auf eine Zelle in einem 1-Zellen-Cage (T065 baut
    //   das implizit über NakedSingle/Cage-Forced — wir lassen den Service entscheiden
    //   welche Strategy passt und prüfen dass keine SolverFallback nötig ist).
    // -------------------------------------------------------------------
    [Fact]
    public async Task T065_GetHint_FilledExceptOneCageCell_UsesDeterministicStrategy()
    {
        var (gameId, solution) = await StartFreshGameAsync(seed: 2065);

        var gameSvc = ServiceFactory.NewGameService(_fx);
        // Wir leeren NUR eine einzige Zelle in der Mitte des Grids — das gibt
        // sowohl NakedSingle als auch CageForced. Wichtig: NICHT SolverFallback.
        for (byte r = 0; r < 9; r++)
        for (byte c = 0; c < 9; c++)
        {
            if (r == 4 && c == 4) continue;
            await gameSvc.SetCellValueAsync(gameId, r, c, solution[r, c]);
        }

        var hint = await ServiceFactory.NewHintService(_fx).GetHintAsync(gameId);

        hint.Strategy.Should().BeOneOf(HintStrategy.NakedSingle, HintStrategy.CageForced);
        hint.Value.Should().Be(solution[4, 4]);
    }
}
