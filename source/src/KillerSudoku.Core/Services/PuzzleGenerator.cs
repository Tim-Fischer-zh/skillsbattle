using KillerSudoku.Core.Abstractions;
using KillerSudoku.Core.Models;

namespace KillerSudoku.Core.Services;

/// <summary>
/// Random Killer-Sudoku generator.
///
/// Two-stage:
///   1. Generate a complete random 9×9 Sudoku solution (backtracking with randomized
///      value-order on each cell — yields a uniformly random valid grid).
///   2. Partition the 81 cells into orthogonally-connected cages whose target sums
///      are derived from the solved grid. Cage size distribution depends on the
///      requested difficulty.
///
/// After partitioning the candidate puzzle is verified against ISolverService for
/// **unique solvability** (CountSolutions == 1). If multiple solutions remain
/// (cages too loose) the partition is retried. As a last resort the solved grid is
/// regenerated.
/// </summary>
public sealed class PuzzleGenerator : IPuzzleGenerator
{
    private readonly ISolverService _solver;

    public PuzzleGenerator(ISolverService solver) => _solver = solver;

    private const int MaxGridRetries = 6;
    private const int MaxCageRetries = 30;

    // Wall-clock-Cap pro Generate-Call. Wenn überschritten → InvalidOperationException
    // damit das UI eine Fehlermeldung zeigt statt ewig zu spinnen.
    private static readonly TimeSpan GenerationTimeout = TimeSpan.FromSeconds(45);

    public PuzzleInputDto Generate(byte difficulty, Random? rng = null)
    {
        if (difficulty is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(difficulty), "Difficulty must be 1..3.");
        rng ??= Random.Shared;

        var (minSize, maxSize, allowSingletons) = SizeBudget(difficulty);
        var deadline = DateTime.UtcNow + GenerationTimeout;

        for (int gridAttempt = 0; gridAttempt < MaxGridRetries; gridAttempt++)
        {
            if (DateTime.UtcNow > deadline) break;

            var solved = GenerateSolvedGrid(rng);

            for (int cageAttempt = 0; cageAttempt < MaxCageRetries; cageAttempt++)
            {
                if (DateTime.UtcNow > deadline) break;

                var cages = PartitionIntoCages(solved, minSize, maxSize, allowSingletons, rng);
                if (cages is null) continue;

                var solveResult = _solver.Solve(new byte[9, 9], cages);
                if (solveResult.Solutions == 1 && solveResult.Solution is not null)
                {
                    // Clues für die UI-Preview im Editor — deterministisch
                    // anhand der Cage-Layout-Hash, damit /play später identisch ist.
                    int clueCount = ClueSelector.CountForDifficulty(difficulty);
                    var clues = ClueSelector.PickClues(solveResult.Solution, cages, clueCount);
                    return new PuzzleInputDto(difficulty, cages, clues);
                }
            }
        }

        throw new InvalidOperationException(
            $"Konnte kein eindeutiges Puzzle für Difficulty {difficulty} erzeugen — bitte erneut versuchen.");
    }

    // -----------------------------------------------------------------------
    // Difficulty → cage size budget
    // -----------------------------------------------------------------------
    private static (int Min, int Max, bool AllowSingletons) SizeBudget(byte difficulty) =>
        difficulty switch
        {
            // Tight ranges so dass der Solver pro Partition meistens unique-solution
            // verifiziert und kaum verworfene Kandidaten produziert.
            1 => (1, 2, true),   // easy   — viele 1-/2-Cell-Cages (= Hint-artig)
            2 => (2, 3, false),  // medium — keine Hint-Cages, kleine Gruppen
            3 => (2, 4, false),  // hard   — keine Hint-Cages, gemischt mit größeren Cages
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
        };

    // =======================================================================
    // Step 1 — random Sudoku solution
    // =======================================================================
    private static byte[,] GenerateSolvedGrid(Random rng)
    {
        var grid = new byte[9, 9];
        if (!FillGrid(grid, 0, rng))
            throw new InvalidOperationException("Backtracking filler failed (should be unreachable).");
        return grid;
    }

    private static bool FillGrid(byte[,] grid, int idx, Random rng)
    {
        if (idx == 81) return true;
        int r = idx / 9, c = idx % 9;
        if (grid[r, c] != 0) return FillGrid(grid, idx + 1, rng);

        Span<byte> values = stackalloc byte[9] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        Shuffle(values, rng);

        foreach (byte v in values)
        {
            if (!IsPlacementLegal(grid, r, c, v)) continue;
            grid[r, c] = v;
            if (FillGrid(grid, idx + 1, rng)) return true;
            grid[r, c] = 0;
        }
        return false;
    }

    private static bool IsPlacementLegal(byte[,] grid, int r, int c, byte v)
    {
        for (int i = 0; i < 9; i++)
        {
            if (grid[r, i] == v) return false;
            if (grid[i, c] == v) return false;
        }
        int br = (r / 3) * 3, bc = (c / 3) * 3;
        for (int rr = br; rr < br + 3; rr++)
            for (int cc = bc; cc < bc + 3; cc++)
                if (grid[rr, cc] == v) return false;
        return true;
    }

    private static void Shuffle<T>(Span<T> span, Random rng)
    {
        for (int i = span.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (span[i], span[j]) = (span[j], span[i]);
        }
    }

    // =======================================================================
    // Step 2 — partition the grid into orthogonally-connected cages
    // =======================================================================
    private static IReadOnlyList<CageInputDto>? PartitionIntoCages(
        byte[,] solvedGrid, int minSize, int maxSize, bool allowSingletons, Random rng)
    {
        var cageOf = new int[81];
        Array.Fill(cageOf, -1);

        var cages = new List<List<(byte Row, byte Col)>>();
        var order = Enumerable.Range(0, 81).ToArray();
        Shuffle(order, rng);

        foreach (int seedIdx in order)
        {
            if (cageOf[seedIdx] != -1) continue;

            int targetSize = rng.Next(minSize, maxSize + 1);
            int cageId = cages.Count;
            var cells = new List<(byte Row, byte Col)>(targetSize);

            int sr = seedIdx / 9, sc = seedIdx % 9;
            cells.Add(((byte)sr, (byte)sc));
            cageOf[seedIdx] = cageId;

            // BFS-ish growth: at each step pick a random unassigned orthogonal neighbour
            while (cells.Count < targetSize)
            {
                var candidates = new List<(byte Row, byte Col)>();
                foreach (var (r, c) in cells)
                {
                    foreach (var (nr, nc) in Neighbours(r, c))
                    {
                        if (cageOf[nr * 9 + nc] == -1)
                            candidates.Add((nr, nc));
                    }
                }
                if (candidates.Count == 0) break;

                var pick = candidates[rng.Next(candidates.Count)];
                cells.Add(pick);
                cageOf[pick.Row * 9 + pick.Col] = cageId;
            }

            cages.Add(cells);
        }

        // Post-process: merge any 1-cell cages into a neighbouring cage if singletons
        // are not allowed for this difficulty.
        if (!allowSingletons)
        {
            for (int i = 0; i < cages.Count; i++)
            {
                if (cages[i].Count != 1) continue;
                if (!TryMergeSingleton(cages, cageOf, i, maxSize)) return null;
            }
        }

        // Build CageInputDtos with sums from the solved grid.
        var result = new List<CageInputDto>(cages.Count);
        foreach (var cells in cages)
        {
            if (cells.Count == 0) continue; // merged-away markers
            int sum = 0;
            foreach (var (r, c) in cells) sum += solvedGrid[r, c];
            if (sum is < 1 or > 45) return null; // defensive
            result.Add(new CageInputDto((byte)sum, cells));
        }
        return result;
    }

    private static IEnumerable<(byte Row, byte Col)> Neighbours(int r, int c)
    {
        if (r > 0) yield return ((byte)(r - 1), (byte)c);
        if (r < 8) yield return ((byte)(r + 1), (byte)c);
        if (c > 0) yield return ((byte)r, (byte)(c - 1));
        if (c < 8) yield return ((byte)r, (byte)(c + 1));
    }

    /// <summary>
    /// Absorb a singleton cage into a neighbouring cage if that cage still has capacity.
    /// Returns false if no merge is possible — the caller should retry the partition.
    /// </summary>
    private static bool TryMergeSingleton(
        List<List<(byte Row, byte Col)>> cages, int[] cageOf, int singletonId, int maxSize)
    {
        var cell = cages[singletonId][0];
        int r = cell.Row, c = cell.Col;

        // Prefer a neighbour whose cage is smallest (keeps size distribution balanced)
        int bestNeighbour = -1;
        int bestSize = int.MaxValue;
        foreach (var (nr, nc) in Neighbours(r, c))
        {
            int neighbourCageId = cageOf[nr * 9 + nc];
            if (neighbourCageId == singletonId) continue;
            int neighbourSize = cages[neighbourCageId].Count;
            if (neighbourSize >= maxSize) continue;
            if (neighbourSize < bestSize)
            {
                bestSize = neighbourSize;
                bestNeighbour = neighbourCageId;
            }
        }
        if (bestNeighbour == -1) return false;

        cages[bestNeighbour].Add(cell);
        cageOf[r * 9 + c] = bestNeighbour;
        cages[singletonId].Clear();
        return true;
    }
}
