using KillerSudoku.Core.Models;

namespace KillerSudoku.Core.Services;

/// <summary>
/// Validiert die strukturellen Bedingungen eines vom User eingegebenen Puzzles (UC04):
///   - Difficulty ∈ {1, 2, 3}
///   - Jede der 81 Zellen ist in genau einer Cage (keine Lücke, kein Overlap)
///   - Cage-Sum ∈ [1, 45]
///   - Cage hat 1..9 Zellen ohne Inner-Duplicates
///   - Σ aller Cage-Sums == 405 (README §2.3 / AC05.4 Pre-Solver-Fast-Fail)
///
/// Diese Validierung läuft VOR dem Solver-Call (UC05 Schritt 1).
/// </summary>
public sealed class PuzzleStructureValidator
{
    public ValidationResult Validate(PuzzleInputDto input)
    {
        if (input is null)
            return new ValidationResult(false, "Puzzle-Input ist null");

        if (input.Difficulty is < 1 or > 3)
            return new ValidationResult(false, "Schwierigkeit muss 1, 2 oder 3 sein");

        if (input.Cages is null || input.Cages.Count == 0)
            return new ValidationResult(false, "Puzzle hat keine Cages");

        var covered = new bool[81];
        int totalSum = 0;

        foreach (var cage in input.Cages)
        {
            if (cage.Sum is < 1 or > 45)
                return new ValidationResult(false, $"Cage-Summe {cage.Sum} außerhalb [1, 45]");

            if (cage.Cells is null || cage.Cells.Count == 0 || cage.Cells.Count > 9)
                return new ValidationResult(false,
                    $"Cage muss 1..9 Zellen haben (hat {cage.Cells?.Count ?? 0})");

            var cellSet = new HashSet<(byte, byte)>();
            foreach (var (r, c) in cage.Cells)
            {
                if (r > 8 || c > 8)
                    return new ValidationResult(false, $"Zelle ({r},{c}) außerhalb des 9×9-Grids");
                if (!cellSet.Add((r, c)))
                    return new ValidationResult(false, $"Zelle ({r},{c}) doppelt in einer Cage");

                int idx = r * 9 + c;
                if (covered[idx])
                    return new ValidationResult(false, $"Zelle ({r},{c}) gehört zu mehreren Cages");
                covered[idx] = true;
            }

            totalSum += cage.Sum;
        }

        for (int i = 0; i < 81; i++)
        {
            if (!covered[i])
                return new ValidationResult(false,
                    $"Zelle ({i / 9},{i % 9}) gehört zu keinem Cage");
        }

        if (totalSum != 405)
            return new ValidationResult(false,
                $"Summe der Cage-Summen muss 405 sein (ist {totalSum}) — README §2.3");

        return new ValidationResult(true, null);
    }
}
