using FluentAssertions;
using KillerSudoku.Core.Models;
using KillerSudoku.Core.Services;
using KillerSudoku.UnitTests.Fixtures;

namespace KillerSudoku.UnitTests;

public class SolverServiceTests
{
    private readonly SolverService _sut = new();
    private readonly SolutionValidator _validator = new();

    // T103 — Trivial-Puzzle (alle Hints außer 1 Zelle) → 1 Lösung
    [Fact]
    public void Solve_OneEmptyCell_FindsUniqueSolution()
    {
        var solution = TrivialSudoku.Grid();
        var givens = (byte[,])solution.Clone();
        givens[4, 4] = 0; // Eine Zelle frei
        var cages = TrivialSudoku.RowCages();

        var result = _sut.Solve(givens, cages);

        result.Solutions.Should().Be(1);
        result.Solution.Should().NotBeNull();
        result.Solution![4, 4].Should().Be(solution[4, 4]);
    }

    // T100/T102 — leeres Grid mit "ganze Reihen sind Cages mit Sum 45" → 1 Lösung
    // (Das Trivial-Sudoku ist nicht die einzige Lösung — wir bauen ein Puzzle wo es genau 1 Lösung gibt.)
    // Wir setzen 81 - 4 Zellen als Givens (TrivialSudoku-Werte). 4 frei in einem Block.
    [Fact]
    public void Solve_4EmptyCellsInOneNonet_FindsUniqueSolution()
    {
        var solution = TrivialSudoku.Grid();
        var givens = (byte[,])solution.Clone();
        // Nonet (0,0) = cells (0..2, 0..2), wir machen 4 davon frei
        givens[0, 0] = 0;
        givens[0, 1] = 0;
        givens[1, 0] = 0;
        givens[1, 1] = 0;
        var cages = TrivialSudoku.RowCages();

        var result = _sut.Solve(givens, cages);

        result.Solutions.Should().Be(1);
        result.Solution.Should().NotBeNull();
        // Verifiziere Lösung gegen Validator
        _validator.Validate(result.Solution!, cages).IsCorrect.Should().BeTrue();
    }

    // T104 — Multi-Solution (komplett leer + lockere Cages → viele Lösungen)
    [Fact]
    public void CountSolutions_EmptyGridWithRowCages_DetectsMultiple()
    {
        // Komplett leeres Grid + nur Row-Cages — es gibt SEHR viele Lösungen
        var givens = new byte[9, 9]; // alle 0
        var cages = TrivialSudoku.RowCages();

        var count = _sut.CountSolutions(givens, cages, limit: 2);

        count.Should().Be(2, "Solver muss bei limit=2 stoppen sobald 2 Lösungen gefunden");
    }

    // T103 — Unsolvable: Cage-Sum total ≠ 405 (impossible nach §2.3)
    [Fact]
    public void Solve_CageSumTotalNot405_ReturnsZeroSolutions()
    {
        var givens = new byte[9, 9];
        // Bauen wir 9 Row-Cages mit Sums 44/45/45.. → Total = 8*45 + 44 = 404
        var cages = new List<CageInputDto>();
        for (byte r = 0; r < 9; r++)
        {
            var cells = new List<(byte, byte)>();
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            cages.Add(new CageInputDto((byte)(r == 0 ? 44 : 45), cells));
        }

        var result = _sut.Solve(givens, cages);

        result.Solutions.Should().Be(0);
        result.Solution.Should().BeNull();
    }

    // T105 — Performance-Boundary: Solver bei realistischem Puzzle < 2 s
    [Fact]
    public void Solve_NearComplete_Under2Seconds()
    {
        var solution = TrivialSudoku.Grid();
        var givens = (byte[,])solution.Clone();
        // 20 Zellen frei → moderate Schwierigkeit
        var rng = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            int r = rng.Next(9), c = rng.Next(9);
            givens[r, c] = 0;
        }
        var cages = TrivialSudoku.RowCages();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = _sut.Solve(givens, cages);
        sw.Stop();

        result.Solutions.Should().BeGreaterThan(0);
        sw.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    // T101 — Example-2: zweites bekanntes Solver-Beispiel hat genau 1 Lösung.
    //   Wir verwenden den deterministischen PuzzleGenerator mit fixem Seed als
    //   "bekanntes Beispiel-2" — Generator validiert vor Rückgabe selbst dass
    //   Solutions == 1.
    [Fact]
    public void T101_Solve_Example2_HasExactlyOneSolution()
    {
        var generator = new PuzzleGenerator(_sut);
        var puzzle = generator.Generate(difficulty: 1, rng: new Random(777));

        var result = _sut.Solve(new byte[9, 9], puzzle.Cages);

        result.Solutions.Should().Be(1);
        result.Solution.Should().NotBeNull();
        _validator.Validate(result.Solution!, puzzle.Cages).IsCorrect.Should().BeTrue();
    }

    // T106 — Cage-Duplikat-Erkennung im Solver-Backtracking.
    //   Cage = [(0,0), (0,1)] mit Sum=10. Givens (0,0)=5 würde (0,1)=5 erzwingen
    //   damit die Cage-Sum stimmt — das verletzt aber das Cage-Distinct-Constraint.
    //   Solver muss den Branch verwerfen → keine Lösung.
    [Fact]
    public void T106_Solve_CageDuplicateBranch_IsRejected()
    {
        var givens = new byte[9, 9];
        givens[0, 0] = 5;
        var cages = new List<CageInputDto>
        {
            new CageInputDto(10, new List<(byte, byte)> { (0, 0), (0, 1) }),
        };
        var row0Rest = new List<(byte, byte)>();
        for (byte c = 2; c < 9; c++) row0Rest.Add(((byte)0, c));
        cages.Add(new CageInputDto(35, row0Rest)); // 45 - 10
        for (byte r = 1; r < 9; r++)
        {
            var cells = new List<(byte, byte)>();
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            cages.Add(new CageInputDto(45, cells));
        }

        var result = _sut.Solve(givens, cages);

        // (0,1) muss 5 sein damit Sum=10, aber Cage-Distinct verbietet das → 0 Lösungen.
        result.Solutions.Should().Be(0);
    }

    // T095 — Cage-Distinct-Constraint: Cage [(0,0),(0,1)] Sum=10 mit givens (0,0)=5 → (0,1) NICHT 5
    [Fact]
    public void Solve_CageDistinctConstraint_ExcludesDuplicateWithinCage()
    {
        // Konstruiere kleines Puzzle mit lockerer Struktur — wir prüfen nur das Constraint via Solver
        var solution = TrivialSudoku.Grid();
        var givens = (byte[,])solution.Clone();
        givens[0, 0] = 0;
        givens[0, 1] = 0;

        // Custom Cages: small 2-cell cage über (0,0)+(0,1) mit Sum=3 (1+2)
        var smallCages = new List<CageInputDto>
        {
            new CageInputDto(3, new List<(byte, byte)> { (0, 0), (0, 1) }),
        };
        // Plus row-cages für rows 1..8
        for (byte r = 1; r < 9; r++)
        {
            var cells = new List<(byte, byte)>();
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            smallCages.Add(new CageInputDto(45, cells));
        }
        // Row 0 muss noch covered sein. (0,0)+(0,1) sind in small cage. Cells (0,2..8) brauchen eine cage.
        var row0Rest = new List<(byte, byte)>();
        for (byte c = 2; c < 9; c++) row0Rest.Add(((byte)0, c));
        smallCages.Add(new CageInputDto(42, row0Rest)); // 45-3 = 42

        var result = _sut.Solve(givens, smallCages);

        result.Solutions.Should().BeGreaterThan(0);
        // Die Lösung muss in (0,0) und (0,1) zwei DIFFERENT Werte haben (Cage-Distinct)
        result.Solution![0, 0].Should().NotBe(result.Solution[0, 1]);
        // Plus: Sum = 3 (= 1+2)
        (result.Solution[0, 0] + result.Solution[0, 1]).Should().Be(3);
    }
}
