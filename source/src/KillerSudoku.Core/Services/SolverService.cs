using System.Numerics;
using KillerSudoku.Core.Abstractions;
using KillerSudoku.Core.Models;

namespace KillerSudoku.Core.Services;

/// <summary>
/// Killer-Sudoku-Solver: Backtracking + Minimum-Remaining-Values (MRV) Heuristik +
/// Constraint-Propagation für Sudoku-Constraints (Row/Col/Nonet) + Cage-Constraints
/// (Cage-Sum-Range + Cage-Distinct).
///
/// Algorithmus-Eckpunkte:
///   - Bitmask-Repräsentation pro Zelle: Bit b für Wert b∈{1..9}
///   - Pre-Check: Σ aller Cage-Sums == 405 (README §2.3 + AC05.4)
///   - Pro Zelle: erlaubte Werte = AllValues AND NOT (RowMask | ColMask | NonetMask | CageUsedMask)
///   - Cage-Sum-Range: bei k unbefüllten Zellen in Cage muss der gewählte Wert v
///     ein verbleibendes Sum-Goal lassen, das mit (k-1) distinct unused digits erreichbar ist
///   - MRV: wähle nächste Zelle mit minimaler Kandidaten-Anzahl
///   - Early-Stop: bricht ab, sobald `limit` Lösungen gefunden (Default 2)
/// </summary>
public sealed class SolverService : ISolverService
{
    private const int AllValues = 0b11_1111_1110; // Bits 1..9

    public SolveResult Solve(byte[,] givens, IReadOnlyList<CageInputDto> cages)
    {
        var ctx = SolverContext.Create(givens, cages);
        if (ctx == null) return new SolveResult(0, null);

        var sols = new List<byte[,]>(2);
        Backtrack(ctx, sols, limit: 2);

        return new SolveResult(
            sols.Count,
            sols.Count == 1 ? sols[0] : null
        );
    }

    public int CountSolutions(byte[,] givens, IReadOnlyList<CageInputDto> cages, int limit = 2)
    {
        var ctx = SolverContext.Create(givens, cages);
        if (ctx == null) return 0;

        var sols = new List<byte[,]>(limit);
        Backtrack(ctx, sols, limit);
        return sols.Count;
    }

    private static void Backtrack(SolverContext ctx, List<byte[,]> sols, int limit)
    {
        if (sols.Count >= limit) return;

        int bestR = -1, bestC = -1, bestCand = 0, bestCount = 10;
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                if (ctx.Grid[r, c] != 0) continue;
                int cand = ctx.CandidatesFor(r, c);
                int count = BitOperations.PopCount((uint)cand);
                if (count == 0) return; // dead end
                if (count < bestCount)
                {
                    bestCount = count;
                    bestR = r; bestC = c; bestCand = cand;
                    if (count == 1) goto BREAK_OUTER; // can't do better
                }
            }
        }
    BREAK_OUTER:

        if (bestR == -1)
        {
            if (ctx.AllCagesSatisfied())
                sols.Add(ctx.Snapshot());
            return;
        }

        int mask = bestCand;
        while (mask != 0)
        {
            int bit = mask & -mask;
            byte v = (byte)BitOperations.TrailingZeroCount((uint)bit);
            mask ^= bit;

            ctx.Assign(bestR, bestC, v);
            Backtrack(ctx, sols, limit);
            ctx.Unassign(bestR, bestC, v);

            if (sols.Count >= limit) return;
        }
    }

    // -----------------------------------------------------------------------
    // Inner mutable solver state
    // -----------------------------------------------------------------------
    private sealed class SolverContext
    {
        public byte[,] Grid = null!;
        public int[] RowMask = null!;
        public int[] ColMask = null!;
        public int[] NonetMask = null!;
        public int[] CageOfCell = null!;     // 81 entries, -1 if no cage
        public int[] CageRemainingSum = null!;
        public int[] CageUsedMask = null!;
        public int[] CageUnfilled = null!;
        public byte[] CageOriginalSum = null!;

        public static SolverContext? Create(byte[,] givens, IReadOnlyList<CageInputDto> cages)
        {
            if (cages.Count == 0) return null;
            int total = 0;
            foreach (var cage in cages) total += cage.Sum;
            if (total != 405) return null;

            var ctx = new SolverContext
            {
                Grid = (byte[,])givens.Clone(),
                RowMask = new int[9],
                ColMask = new int[9],
                NonetMask = new int[9],
                CageOfCell = new int[81],
                CageRemainingSum = new int[cages.Count],
                CageUsedMask = new int[cages.Count],
                CageUnfilled = new int[cages.Count],
                CageOriginalSum = new byte[cages.Count],
            };

            Array.Fill(ctx.CageOfCell, -1);

            for (int i = 0; i < cages.Count; i++)
            {
                ctx.CageOriginalSum[i] = cages[i].Sum;
                ctx.CageRemainingSum[i] = cages[i].Sum;
                ctx.CageUnfilled[i] = cages[i].Cells.Count;
                foreach (var (r, c) in cages[i].Cells)
                {
                    int idx = r * 9 + c;
                    if (ctx.CageOfCell[idx] != -1) return null; // overlap
                    ctx.CageOfCell[idx] = i;
                }
            }

            // Process givens
            for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
            {
                byte v = ctx.Grid[r, c];
                if (v == 0) continue;
                if (v < 1 || v > 9) return null;

                int bit = 1 << v;
                if ((ctx.RowMask[r] & bit) != 0) return null;
                if ((ctx.ColMask[c] & bit) != 0) return null;
                int nonet = (r / 3) * 3 + c / 3;
                if ((ctx.NonetMask[nonet] & bit) != 0) return null;

                int cageId = ctx.CageOfCell[r * 9 + c];
                if (cageId < 0) return null;
                if ((ctx.CageUsedMask[cageId] & bit) != 0) return null;

                ctx.RowMask[r] |= bit;
                ctx.ColMask[c] |= bit;
                ctx.NonetMask[nonet] |= bit;
                ctx.CageUsedMask[cageId] |= bit;
                ctx.CageRemainingSum[cageId] -= v;
                ctx.CageUnfilled[cageId]--;
                if (ctx.CageRemainingSum[cageId] < 0) return null;
            }

            return ctx;
        }

        public int CandidatesFor(int r, int c)
        {
            int used = RowMask[r] | ColMask[c] | NonetMask[(r / 3) * 3 + c / 3];
            int cageId = CageOfCell[r * 9 + c];
            if (cageId < 0) return 0;
            int cageUsed = CageUsedMask[cageId];
            used |= cageUsed;

            int unfilled = CageUnfilled[cageId];
            int remaining = CageRemainingSum[cageId];

            int allCand = AllValues & ~used;
            if (allCand == 0) return 0;

            // Cage-Sum-Range Pruning
            if (unfilled == 1)
            {
                if (remaining < 1 || remaining > 9) return 0;
                int onlyBit = 1 << remaining;
                return (allCand & onlyBit);
            }

            // Pool of digits still available within this cage (excluding cageUsed and this cell's masks)
            int availableInCage = AllValues & ~cageUsed;
            int restSlots = unfilled - 1;

            int validCand = 0;
            int iter = allCand;
            while (iter != 0)
            {
                int bit = iter & -iter;
                int v = BitOperations.TrailingZeroCount((uint)bit);
                iter ^= bit;
                int sumLeft = remaining - v;
                if (sumLeft < 0) continue;

                int pool = availableInCage & ~bit;
                int minSum = SumOfNSmallest(pool, restSlots);
                int maxSum = SumOfNLargest(pool, restSlots);
                if (minSum < 0 || maxSum < 0) continue;
                if (sumLeft >= minSum && sumLeft <= maxSum)
                    validCand |= bit;
            }
            return validCand;
        }

        private static int SumOfNSmallest(int mask, int n)
        {
            int sum = 0, count = 0;
            for (int v = 1; v <= 9 && count < n; v++)
                if ((mask & (1 << v)) != 0) { sum += v; count++; }
            return count >= n ? sum : -1;
        }

        private static int SumOfNLargest(int mask, int n)
        {
            int sum = 0, count = 0;
            for (int v = 9; v >= 1 && count < n; v--)
                if ((mask & (1 << v)) != 0) { sum += v; count++; }
            return count >= n ? sum : -1;
        }

        public void Assign(int r, int c, byte v)
        {
            Grid[r, c] = v;
            int bit = 1 << v;
            RowMask[r] |= bit;
            ColMask[c] |= bit;
            NonetMask[(r / 3) * 3 + c / 3] |= bit;
            int cageId = CageOfCell[r * 9 + c];
            CageUsedMask[cageId] |= bit;
            CageRemainingSum[cageId] -= v;
            CageUnfilled[cageId]--;
        }

        public void Unassign(int r, int c, byte v)
        {
            Grid[r, c] = 0;
            int bit = 1 << v;
            RowMask[r] &= ~bit;
            ColMask[c] &= ~bit;
            NonetMask[(r / 3) * 3 + c / 3] &= ~bit;
            int cageId = CageOfCell[r * 9 + c];
            CageUsedMask[cageId] &= ~bit;
            CageRemainingSum[cageId] += v;
            CageUnfilled[cageId]++;
        }

        public bool AllCagesSatisfied()
        {
            for (int i = 0; i < CageOriginalSum.Length; i++)
            {
                if (CageUnfilled[i] != 0) return false;
                if (CageRemainingSum[i] != 0) return false;
            }
            return true;
        }

        public byte[,] Snapshot() => (byte[,])Grid.Clone();
    }
}
