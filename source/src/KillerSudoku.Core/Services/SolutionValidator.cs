using KillerSudoku.Core.Models;

namespace KillerSudoku.Core.Services;

/// <summary>
/// Validiert eine vollständig befüllte 9×9-Killer-Sudoku-Lösung gegen alle vier
/// Constraint-Klassen (Row / Column / Nonet / Cage) sowie den README-§2.3-Sum-Check (405).
/// Prüf-Reihenfolge (Fast-Fail, vom günstigsten zum teuersten Check):
///   1. Incomplete (Zelle &lt; 1 oder &gt; 9 → kein finaler Wert)
///   2. Sum-Check 405 (Σ aller Zellen)
///   3. Row 1–9 exactly once
///   4. Column 1–9 exactly once
///   5. Nonet (3×3) 1–9 exactly once
///   6. Cage: Σ matcht und keine Duplikate innerhalb der Cage
///
/// Quelle der Constraints: README §1 (4 Regeln wörtlich) + §2.3 (Sum-Check).
/// </summary>
public sealed class SolutionValidator
{
    public CheckResult Validate(byte[,] grid, IReadOnlyList<CageInputDto> cages)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(cages);

        // (1) Incomplete: irgendeine Zelle nicht 1..9 → frühen Abbruch
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            if (grid[r, c] < 1 || grid[r, c] > 9)
                return new CheckResult(false, CheckFailReason.Incomplete);

        // (2) Sum-Check 405 — README §2.3 "value can be determined unambiguously"
        int total = 0;
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            total += grid[r, c];
        if (total != 405)
            return new CheckResult(false, CheckFailReason.SumMismatch);

        // (3) Row 1..9 exactly once
        for (int r = 0; r < 9; r++)
        {
            int mask = 0;
            for (int c = 0; c < 9; c++)
            {
                int bit = 1 << grid[r, c];
                if ((mask & bit) != 0)
                    return new CheckResult(false, CheckFailReason.RowDuplicate);
                mask |= bit;
            }
        }

        // (4) Column 1..9 exactly once
        for (int c = 0; c < 9; c++)
        {
            int mask = 0;
            for (int r = 0; r < 9; r++)
            {
                int bit = 1 << grid[r, c];
                if ((mask & bit) != 0)
                    return new CheckResult(false, CheckFailReason.ColumnDuplicate);
                mask |= bit;
            }
        }

        // (5) Nonet 1..9 exactly once
        for (int br = 0; br < 3; br++)
        for (int bc = 0; bc < 3; bc++)
        {
            int mask = 0;
            for (int dr = 0; dr < 3; dr++)
            for (int dc = 0; dc < 3; dc++)
            {
                int bit = 1 << grid[br * 3 + dr, bc * 3 + dc];
                if ((mask & bit) != 0)
                    return new CheckResult(false, CheckFailReason.NonetDuplicate);
                mask |= bit;
            }
        }

        // (6) Cage: Sum + Distinct innerhalb Cage
        foreach (var cage in cages)
        {
            int sum = 0;
            int mask = 0;
            foreach (var (row, col) in cage.Cells)
            {
                byte v = grid[row, col];
                int bit = 1 << v;
                if ((mask & bit) != 0)
                    return new CheckResult(false, CheckFailReason.CageDuplicate);
                mask |= bit;
                sum += v;
            }
            if (sum != cage.Sum)
                return new CheckResult(false, CheckFailReason.CageSumMismatch);
        }

        return new CheckResult(true, null);
    }
}
