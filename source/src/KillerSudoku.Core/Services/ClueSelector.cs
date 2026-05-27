using KillerSudoku.Core.Models;

namespace KillerSudoku.Core.Services;

/// <summary>
/// Pre-fills initial clue cells for a Killer-Sudoku puzzle. Used by both
/// <see cref="PuzzleGenerator"/> (to preview clues in the editor right after
/// generation) and <see cref="KillerSudoku.Data.Services.GameService"/> (to
/// initialize <c>GameCell</c> rows on the first <c>StartGameAsync</c>).
///
/// <para>
/// The selection is deterministic per cage layout: a hash of the cages drives
/// the RNG. That guarantees the editor preview and the actual /play view show
/// the *same* prefilled cells — fairness for the highscore and consistency
/// across reloads.
/// </para>
/// </summary>
public static class ClueSelector
{
    /// <summary>
    /// Number of prefilled cells per difficulty.
    /// 1 = 20 clues (easy), 2 = 8 clues (medium), 3 = 0 clues (hard / classic Killer).
    /// </summary>
    public static int CountForDifficulty(byte difficulty) => difficulty switch
    {
        1 => 20,
        2 => 8,
        _ => 0,
    };

    /// <summary>
    /// Picks <paramref name="count"/> cells from <paramref name="solution"/> using a
    /// cage-layout-deterministic RNG. Returns an empty list if count == 0.
    /// </summary>
    public static IReadOnlyList<ClueDto> PickClues(
        byte[,] solution,
        IReadOnlyList<CageInputDto> cages,
        int count)
    {
        if (count <= 0) return Array.Empty<ClueDto>();

        var rng = new Random(ComputeCageSeed(cages));
        var indices = Enumerable.Range(0, 81).ToList();
        // Fisher-Yates shuffle
        for (int i = 80; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var result = new List<ClueDto>(count);
        foreach (var idx in indices.Take(count))
        {
            byte r = (byte)(idx / 9);
            byte c = (byte)(idx % 9);
            result.Add(new ClueDto(r, c, solution[r, c]));
        }
        return result;
    }

    /// <summary>
    /// Deterministic seed derived from cage sums + their anchor cells.
    /// Stable across processes — different Puzzle.Ids that share the same cage
    /// layout produce identical clues.
    /// </summary>
    private static int ComputeCageSeed(IReadOnlyList<CageInputDto> cages)
    {
        unchecked
        {
            int seed = 17;
            foreach (var cage in cages.OrderBy(c => c.Cells.Min(x => x.Row * 9 + x.Col)))
            {
                int anchor = cage.Cells.Min(c => c.Row * 9 + c.Col);
                seed = seed * 31 + cage.Sum;
                seed = seed * 31 + anchor;
                seed = seed * 31 + cage.Cells.Count;
            }
            return seed;
        }
    }
}
