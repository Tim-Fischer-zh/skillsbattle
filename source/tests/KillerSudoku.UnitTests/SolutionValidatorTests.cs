using FluentAssertions;
using KillerSudoku.Core.Models;
using KillerSudoku.Core.Services;
using KillerSudoku.UnitTests.Fixtures;

namespace KillerSudoku.UnitTests;

public class SolutionValidatorTests
{
    private readonly SolutionValidator _sut = new();

    // T072 — Positive: korrekte Lösung → IsCorrect=true
    [Fact]
    public void Validate_TrivialSudokuMitRowCages_IsCorrect()
    {
        var grid = TrivialSudoku.Grid();
        var cages = TrivialSudoku.RowCages();

        var r = _sut.Validate(grid, cages);

        r.IsCorrect.Should().BeTrue();
        r.FailReason.Should().BeNull();
    }

    // T073 — Sum != 405 → Fast-Fail, SumMismatch
    [Fact]
    public void Validate_SumNot405_ReturnsSumMismatch()
    {
        var grid = TrivialSudoku.WithCell(TrivialSudoku.Grid(), 0, 0, 9); // war 1, jetzt 9 → sum 413
        var cages = TrivialSudoku.RowCages();

        var r = _sut.Validate(grid, cages);

        r.IsCorrect.Should().BeFalse();
        r.FailReason.Should().Be(CheckFailReason.SumMismatch);
    }

    // T079 / T088 — Incomplete (Zelle = 0 / unbefüllt)
    [Fact]
    public void Validate_IncompleteGrid_ReturnsIncomplete()
    {
        var grid = TrivialSudoku.WithCell(TrivialSudoku.Grid(), 0, 0, 0);
        var cages = TrivialSudoku.RowCages();

        var r = _sut.Validate(grid, cages);

        r.IsCorrect.Should().BeFalse();
        r.FailReason.Should().Be(CheckFailReason.Incomplete);
    }

    // T083 — Row-Duplicate (swap (0,0) ↔ (1,0), Sum bleibt 405)
    [Fact]
    public void Validate_RowDuplicate_ReturnsRowDuplicate()
    {
        // TrivialSudoku: row 0 = 1..9, row 1 = 4,5,6,7,8,9,1,2,3
        // Nach swap(0,0,1,0): row 0[0]=4 (Duplikat zu vorhandener 4 in row 0[3]),
        //                    row 1[0]=1 (Duplikat zu vorhandener 1 in row 1[6])
        var grid = TrivialSudoku.WithSwap(TrivialSudoku.Grid(), 0, 0, 1, 0);
        var cages = TrivialSudoku.RowCages();

        var r = _sut.Validate(grid, cages);

        r.IsCorrect.Should().BeFalse();
        r.FailReason.Should().Be(CheckFailReason.RowDuplicate);
    }

    // T084 — Column-Duplicate (swap (0,0) ↔ (0,3), Sum bleibt 405)
    [Fact]
    public void Validate_ColumnDuplicate_ReturnsColumnDuplicate()
    {
        // Row 0 nach swap: [4,2,3,1,5,6,7,8,9] — alle different (kein Row-Duplikat)
        // Col 0 = [4,4,7,2,5,8,3,6,9] → Duplikat 4
        var grid = TrivialSudoku.WithSwap(TrivialSudoku.Grid(), 0, 0, 0, 3);
        var cages = TrivialSudoku.RowCages();

        var r = _sut.Validate(grid, cages);

        r.IsCorrect.Should().BeFalse();
        r.FailReason.Should().Be(CheckFailReason.ColumnDuplicate);
    }

    // T086 — Cage-Sum-Mismatch (Cage erwartet 45, wir setzen 44)
    [Fact]
    public void Validate_CageSumMismatch_ReturnsCageSumMismatch()
    {
        var grid = TrivialSudoku.Grid();
        // Modify cage[0] to expect 44 (off by one), trigger CageSumMismatch
        var rowCells = new List<(byte Row, byte Col)>();
        for (byte c = 0; c < 9; c++) rowCells.Add(((byte)0, c));
        var cages = new List<CageInputDto>
        {
            new CageInputDto(44, rowCells), // Sum-Mismatch (echtes 45)
        };
        // Add remaining rows to cover all 81 cells
        for (byte r = 1; r < 9; r++)
        {
            var cells = new List<(byte Row, byte Col)>();
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            cages.Add(new CageInputDto(45, cells));
        }

        var r2 = _sut.Validate(grid, cages);

        r2.IsCorrect.Should().BeFalse();
        r2.FailReason.Should().Be(CheckFailReason.CageSumMismatch);
    }

    // T087 — Cage-Duplicate: zwei Zellen mit gleichem Wert in einer Cage,
    //         AUCH wenn Sudoku-Regeln das eigentlich erlauben würden (Cage verbietet es trotzdem)
    //
    // Konstruktion: TrivialSudoku, aber definiere eine Cage über (0,0)=1 und (1,1)=5 ... nope, must be duplicate.
    // Wir nehmen (0,0)=1 und (1,6)=1 — beides 1 im TrivialSudoku!
    // T085 — Nonet-Duplicate Detection.
    //   Shift-by-1 Latin Square: grid[r,c] = (r + c) % 9 + 1.
    //     Row 0: 1 2 3 4 5 6 7 8 9
    //     Row 1: 2 3 4 5 6 7 8 9 1
    //     Row 2: 3 4 5 6 7 8 9 1 2
    //     ...
    //   Each row contains 1-9, each column contains 1-9 (valid Latin square),
    //   Σ = 9 × 45 = 405 (sum-check passes).
    //   BUT nonet 0 (rows 0-2, cols 0-2) = {1,2,3,2,3,4,3,4,5} → duplicates → invalid.
    [Fact]
    public void T085_Validate_NonetDuplicate_ReturnsNonetDuplicate()
    {
        var grid = new byte[9, 9];
        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
                grid[r, c] = (byte)((r + c) % 9 + 1);

        var cages = TrivialSudoku.RowCages();
        var result = _sut.Validate(grid, cages);

        result.IsCorrect.Should().BeFalse();
        result.FailReason.Should().Be(CheckFailReason.NonetDuplicate);
    }

    [Fact]
    public void Validate_CageDuplicate_ReturnsCageDuplicate()
    {
        var grid = TrivialSudoku.Grid();
        // (0,0)=1 und (1,6)=1: beides Wert 1
        var dupCage = new CageInputDto(2, new List<(byte, byte)> { (0, 0), (1, 6) });
        // Mock: Single dup-cage. Coverage of all 81 cells isn't required for this isolated test —
        // validator iterates über cages, findet Duplikat, returns früh.
        var r = _sut.Validate(grid, new List<CageInputDto> { dupCage });

        r.IsCorrect.Should().BeFalse();
        r.FailReason.Should().Be(CheckFailReason.CageDuplicate);
    }
}
