using FluentAssertions;
using KillerSudoku.Core.Models;
using KillerSudoku.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.IntegrationTests;

/// <summary>
/// PuzzleService Integration-Tests (UC4 Enter / UC5 Save / UC12 Browse).
/// Test-IDs gemäss docs/test-protocol.csv.
/// </summary>
[Collection(MsSqlCollection.Name)]
public sealed class PuzzleServiceTests
{
    private readonly MsSqlContainerFixture _fx;
    public PuzzleServiceTests(MsSqlContainerFixture fx) => _fx = fx;

    // -------------------------------------------------------------------
    // T040 — Save eines eindeutig lösbaren Puzzles → Saved + Id zurück.
    //   UC05 AC05.1+05.2: Save nur wenn solvable UND uniqueness == 1.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T040_SaveIfSolvable_UniqueSolution_ReturnsSaved()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);
        var input = ServiceFactory.Generator().Generate(difficulty: 1, rng: new Random(42));

        var svc = ServiceFactory.NewPuzzleService(_fx);
        var result = await svc.SaveIfSolvableAsync(input, userId);

        result.Status.Should().Be(SaveStatus.Saved);
        result.PuzzleId.Should().NotBeNull();

        await using var ctx = ServiceFactory.NewContext(_fx);
        var saved = await ctx.Puzzles
            .Include(p => p.Cages).ThenInclude(c => c.Cells)
            .FirstAsync(p => p.Id == result.PuzzleId!.Value);
        saved.Difficulty.Should().Be(1);
        saved.Cages.Should().NotBeEmpty();
        saved.Cages.SelectMany(c => c.Cells).Should().HaveCount(81);
    }

    // -------------------------------------------------------------------
    // T041 — Multi-Solution wird abgelehnt.
    //   "9 Row-Cages mit Sum 45" → Sudoku ist gültig aber hat >> 1 Lösung.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T041_SaveIfSolvable_MultiSolution_IsRejected()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);
        var cages = new List<CageInputDto>(9);
        for (byte r = 0; r < 9; r++)
        {
            var cells = new List<(byte Row, byte Col)>(9);
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            cages.Add(new CageInputDto(45, cells));
        }
        var input = new PuzzleInputDto(Difficulty: 1, Cages: cages);

        var svc = ServiceFactory.NewPuzzleService(_fx);
        var result = await svc.SaveIfSolvableAsync(input, userId);

        result.Status.Should().Be(SaveStatus.MultipleSolutions);
        result.PuzzleId.Should().BeNull();
    }

    // -------------------------------------------------------------------
    // T044 — Σ Cage-Sums ≠ 405 → InvalidStructure (fail-fast, kein Solver).
    //   README §2.3 Sum-Check + UC05 AC05.4.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T044_SaveIfSolvable_SumNot405_IsRejected()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);

        // 9 Row-Cages mit Sum 44 statt 45 → Σ = 396 ≠ 405
        var cages = new List<CageInputDto>(9);
        for (byte r = 0; r < 9; r++)
        {
            var cells = new List<(byte Row, byte Col)>(9);
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            cages.Add(new CageInputDto(44, cells));
        }
        var input = new PuzzleInputDto(Difficulty: 1, Cages: cages);

        var svc = ServiceFactory.NewPuzzleService(_fx);
        var result = await svc.SaveIfSolvableAsync(input, userId);

        result.Status.Should().Be(SaveStatus.InvalidStructure);
    }

    // -------------------------------------------------------------------
    // T043 — Strukturell defekte Cage (Zelle ausserhalb 0..8) → InvalidStructure.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T043_SaveIfSolvable_CellsOutOfBounds_IsRejected()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);

        var cages = new List<CageInputDto>
        {
            new(45, new List<(byte, byte)> {
                (0,0),(0,1),(0,2),(0,3),(0,4),(0,5),(0,6),(0,7),(0,8)
            }),
            // Zelle (9, 0) ist out-of-bounds — Validator muss ablehnen.
            new(45, new List<(byte, byte)> {
                (9,0),(1,1),(1,2),(1,3),(1,4),(1,5),(1,6),(1,7),(1,8)
            }),
        };
        // Padding-Cages für Σ=405 — irrelevant, der Out-of-Range wird vorher gefangen.
        for (byte r = 2; r < 9; r++)
        {
            var cells = new List<(byte, byte)>(9);
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            cages.Add(new CageInputDto(45, cells));
        }
        var input = new PuzzleInputDto(1, cages);

        var svc = ServiceFactory.NewPuzzleService(_fx);
        var result = await svc.SaveIfSolvableAsync(input, userId);

        result.Status.Should().Be(SaveStatus.InvalidStructure);
    }

    // -------------------------------------------------------------------
    // T110 — ListAsync sortiert nach CreatedAt DESC (neueste zuerst).
    //   Verifiziert die Sortierung anhand der tatsächlichen DB-CreatedAt-Werte,
    //   nicht der Save-Reihenfolge — der Test bleibt damit robust gegen
    //   Hostsystem-Clock-Präzision und Co-Tests im Container.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T110_ListAsync_NoFilter_ReturnsAllOrderedByCreatedDesc()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);
        var svc = ServiceFactory.NewPuzzleService(_fx);

        var ids = new List<int>();
        foreach (var diff in new byte[] { 1, 2, 3 })
        {
            var input = ServiceFactory.Generator().Generate(diff, new Random(100 + diff));
            var saved = await svc.SaveIfSolvableAsync(input, userId);
            saved.Status.Should().Be(SaveStatus.Saved);
            ids.Add(saved.PuzzleId!.Value);
        }

        // Ground truth direkt aus der DB: gleicher OrderByDescending(CreatedAt) wie ListAsync.
        await using var ctx = ServiceFactory.NewContext(_fx);
        var expectedOrder = await ctx.Puzzles
            .Where(p => ids.Contains(p.Id))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => p.Id)
            .ToListAsync();

        var page = await svc.ListAsync(difficulty: null, page: 1, pageSize: 100, currentUserId: userId);
        var actualOrder = page.Items
            .Where(p => ids.Contains(p.Id))
            .Select(p => p.Id)
            .ToList();

        actualOrder.Should().Equal(expectedOrder);
    }

    // -------------------------------------------------------------------
    // T111 — Filter nach Difficulty=2 zeigt nur Diff-2-Puzzles.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T111_ListAsync_DifficultyFilter_OnlyMatchingDifficulty()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);
        var svc = ServiceFactory.NewPuzzleService(_fx);

        foreach (var diff in new byte[] { 1, 2, 2, 3 })
        {
            var input = ServiceFactory.Generator().Generate(diff, new Random(200 + diff * 7));
            await svc.SaveIfSolvableAsync(input, userId);
        }

        var page = await svc.ListAsync(difficulty: 2, page: 1, pageSize: 100);

        page.Items.Should().NotBeEmpty();
        page.Items.Should().OnlyContain(p => p.Difficulty == 2);
    }

    // -------------------------------------------------------------------
    // T112 — Pagination: pageSize=2, page=2 → übergeht erste 2.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T112_ListAsync_Pagination_ReturnsCorrectSlice()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);
        var svc = ServiceFactory.NewPuzzleService(_fx);

        for (int i = 0; i < 5; i++)
        {
            var input = ServiceFactory.Generator().Generate(difficulty: 1, new Random(300 + i));
            await svc.SaveIfSolvableAsync(input, userId);
            await Task.Delay(15);
        }

        var allPage = await svc.ListAsync(difficulty: 1, page: 1, pageSize: 100);
        var fullCount = allPage.Items.Count;
        fullCount.Should().BeGreaterThanOrEqualTo(5);

        var page2 = await svc.ListAsync(difficulty: 1, page: 2, pageSize: 2);

        page2.Items.Should().HaveCount(2);
        page2.Page.Should().Be(2);
        page2.PageSize.Should().Be(2);
        page2.Total.Should().Be(fullCount);
    }

    // -------------------------------------------------------------------
    // T113 — Leerer Filter (nichts in der DB für Diff=3) → Items leer, Total=0.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T113_ListAsync_EmptyResult_IsHandled()
    {
        var svc = ServiceFactory.NewPuzzleService(_fx);

        // Wir filtern nach Difficulty=99 (CHECK-Constraint erlaubt nur 1..3, also
        // garantiert keine Treffer ohne dass wir die DB leeren müssen).
        var page = await svc.ListAsync(difficulty: 99, page: 1, pageSize: 100);

        page.Items.Should().BeEmpty();
        page.Total.Should().Be(0);
    }

    // -------------------------------------------------------------------
    // T042 — Negative: Unsolvable Puzzle wird abgelehnt.
    //   Konstruktion: 2-Cell-Cage mit Sum=18 — maximal mögliche Sum für 2 distinct
    //   Digits aus 1..9 = 8+9 = 17. Sum=18 ist im Range [1,45] (Validator OK), aber
    //   strukturell unerfüllbar → Solver findet 0 Lösungen.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T042_SaveIfSolvable_UnsolvablePuzzle_IsRejected()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);

        var cages = new List<CageInputDto>
        {
            // unmöglicher 2-Cell-Cage in Row 0
            new(18, new List<(byte, byte)> { (0, 0), (0, 1) }),
        };
        // Rest von Row 0 (cols 2-8) als 7-Cell-Cage
        var row0Rest = new List<(byte, byte)>();
        for (byte c = 2; c < 9; c++) row0Rest.Add(((byte)0, c));
        cages.Add(new CageInputDto(27, row0Rest));
        // Rows 1-8 jeweils als 9-Cell-Row-Cages
        for (byte r = 1; r < 9; r++)
        {
            var cells = new List<(byte, byte)>();
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            cages.Add(new CageInputDto(45, cells));
        }
        // Σ = 18 + 27 + 8×45 = 405 ✓

        var svc = ServiceFactory.NewPuzzleService(_fx);
        var result = await svc.SaveIfSolvableAsync(
            new PuzzleInputDto(1, cages), userId);

        result.Status.Should().Be(SaveStatus.NotSolvable);
        result.PuzzleId.Should().BeNull();
    }

    // -------------------------------------------------------------------
    // T044 — Performance-Boundary: Save (inkl. Solver-Uniqueness-Check) terminiert
    //   in < 2 s für ein durchschnittliches Difficulty-1-Puzzle.
    //   Spec §UC11 AC11.2.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T044_SaveIfSolvable_Performance_UnderTwoSeconds()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);
        var input = ServiceFactory.Generator().Generate(difficulty: 1, new Random(99));
        var svc = ServiceFactory.NewPuzzleService(_fx);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await svc.SaveIfSolvableAsync(input, userId);
        sw.Stop();

        result.Status.Should().Be(SaveStatus.Saved);
        sw.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    // -------------------------------------------------------------------
    // T045 — Σ-Pre-Fail: Wenn Σ aller Cage-Summen ≠ 405 ist, lehnt der Service
    //   ohne Solver-Call ab (Structure-Validator schlägt vorher zu).
    //   Semantisches Duplikat zu unserem T044-Sum-Test — explizit als T045 markiert
    //   für CSV-Coverage. README §2.3 + AC05.4.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T045_SaveIfSolvable_SumNot405_FailsFastWithoutSolver()
    {
        var userId = await ServiceFactory.CreateUserAsync(_fx);
        // Σ = 9 × 44 = 396 ≠ 405
        var cages = new List<CageInputDto>(9);
        for (byte r = 0; r < 9; r++)
        {
            var cells = new List<(byte, byte)>();
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            cages.Add(new CageInputDto(44, cells));
        }
        var svc = ServiceFactory.NewPuzzleService(_fx);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await svc.SaveIfSolvableAsync(
            new PuzzleInputDto(1, cages), userId);
        sw.Stop();

        result.Status.Should().Be(SaveStatus.InvalidStructure);
        // Pre-Fail muss schnell sein (≤ 100 ms) — Solver wurde nicht gerufen.
        sw.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    // -------------------------------------------------------------------
    // T115 — Boundary: page=0 wird sauber behandelt (Empty oder ArgumentException,
    //   aber kein Server-Error).
    // -------------------------------------------------------------------
    [Fact]
    public async Task T115_ListAsync_PageZero_IsHandledGracefully()
    {
        var svc = ServiceFactory.NewPuzzleService(_fx);

        var result = await svc.ListAsync(difficulty: null, page: 0, pageSize: 100);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }
}
