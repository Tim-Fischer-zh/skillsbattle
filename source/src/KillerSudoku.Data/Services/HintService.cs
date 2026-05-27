using KillerSudoku.Core.Abstractions;
using KillerSudoku.Core.Models;
using KillerSudoku.Data.Entities;
using KillerSudoku.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.Data.Services;

/// <summary>
/// UC07 — Hint-Service. Strategie-Hierarchie:
///   A) Naked-Single — eine Zelle mit nur 1 möglichen Wert
///   B) Cage-Forced — eine Zelle in einem Cage, die durch Cage-Sum-Range eindeutig ist
///   C) Solver-Fallback — beliebige leere Zelle aus der eindeutigen Solver-Lösung
///
/// Pre-Condition (UC07 AC07.3): Game läuft + Grid nicht vollständig befüllt.
/// </summary>
public sealed class HintService : IHintService
{
    private readonly SudokuDbContext _db;
    private readonly ISolverService _solver;
    private readonly TimeProvider _clock;

    public HintService(SudokuDbContext db, ISolverService solver, TimeProvider? clock = null)
    {
        _db = db;
        _solver = solver;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<HintResult> GetHintAsync(int gameId, CancellationToken ct = default)
    {
        var game = await _db.Games
            .Include(g => g.Cells)
            .Include(g => g.Puzzle).ThenInclude(p => p.Cages).ThenInclude(c => c.Cells)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct)
            ?? throw new InvalidOperationException("Game nicht gefunden");

        if (game.IsCompleted)
            throw new InvalidOperationException("Game ist abgeschlossen");

        var grid = new byte[9, 9];
        int filledCount = 0;
        foreach (var gc in game.Cells)
        {
            if (gc.CellValue.HasValue)
            {
                grid[gc.RowIdx, gc.ColIdx] = gc.CellValue.Value;
                filledCount++;
            }
        }

        if (filledCount >= 81)
            throw new InvalidOperationException("Grid ist vollständig — Hint nicht verfügbar (V11)");

        var cages = game.Puzzle.Cages
            .Select(c => new CageInputDto(
                c.Sum,
                c.Cells.Select(cc => (cc.RowIdx, cc.ColIdx)).ToList()))
            .ToList();

        // Solver gibt die eindeutige Lösung zurück
        var solveResult = _solver.Solve(grid, cages);
        if (solveResult.Solutions != 1 || solveResult.Solution is null)
            throw new InvalidOperationException(
                "Aktueller Game-State ist nicht (mehr) eindeutig lösbar — Hint nicht verfügbar");

        var solution = solveResult.Solution;

        // Strategy A — Naked Single: leere Zelle wo nur 1 Wert via Sudoku-Constraints möglich
        var (nsRow, nsCol) = FindNakedSingle(grid);
        if (nsRow >= 0)
        {
            byte v = solution[nsRow, nsCol];
            await ApplyHintAsync(game, (byte)nsRow, (byte)nsCol, v, ct);
            return new HintResult((byte)nsRow, (byte)nsCol, v, HintStrategy.NakedSingle);
        }

        // Strategy B — Cage-Forced: Cage mit 1 unbesetzten Zelle → Sum-Differenz zwingt Wert
        var (cfRow, cfCol) = FindCageForced(grid, cages);
        if (cfRow >= 0)
        {
            byte v = solution[cfRow, cfCol];
            await ApplyHintAsync(game, (byte)cfRow, (byte)cfCol, v, ct);
            return new HintResult((byte)cfRow, (byte)cfCol, v, HintStrategy.CageForced);
        }

        // Fallback — beliebige leere Zelle aus der Lösung
        for (byte r = 0; r < 9; r++)
        for (byte c = 0; c < 9; c++)
        {
            if (grid[r, c] == 0)
            {
                byte v = solution[r, c];
                await ApplyHintAsync(game, r, c, v, ct);
                return new HintResult(r, c, v, HintStrategy.SolverFallback);
            }
        }

        // Sollte nicht erreichbar sein
        throw new InvalidOperationException("Keine leere Zelle gefunden");
    }

    private static (int Row, int Col) FindNakedSingle(byte[,] grid)
    {
        int[] rowMask = new int[9], colMask = new int[9], nonetMask = new int[9];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (grid[r, c] == 0) continue;
            int bit = 1 << grid[r, c];
            rowMask[r] |= bit;
            colMask[c] |= bit;
            nonetMask[(r / 3) * 3 + c / 3] |= bit;
        }

        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (grid[r, c] != 0) continue;
            int used = rowMask[r] | colMask[c] | nonetMask[(r / 3) * 3 + c / 3];
            int cand = 0b11_1111_1110 & ~used;
            if (System.Numerics.BitOperations.PopCount((uint)cand) == 1)
                return (r, c);
        }
        return (-1, -1);
    }

    private static (int Row, int Col) FindCageForced(byte[,] grid, IReadOnlyList<CageInputDto> cages)
    {
        foreach (var cage in cages)
        {
            int unfilledCount = 0;
            (byte R, byte C) unfilledCell = default;
            int sum = 0;
            foreach (var (r, c) in cage.Cells)
            {
                if (grid[r, c] == 0) { unfilledCount++; unfilledCell = (r, c); }
                else sum += grid[r, c];
            }
            if (unfilledCount == 1)
            {
                int needed = cage.Sum - sum;
                if (needed is >= 1 and <= 9)
                    return (unfilledCell.R, unfilledCell.C);
            }
        }
        return (-1, -1);
    }

    private async Task ApplyHintAsync(Game game, byte row, byte col, byte value, CancellationToken ct)
    {
        var cell = await _db.GameCells.FirstAsync(
            gc => gc.GameId == game.Id && gc.RowIdx == row && gc.ColIdx == col, ct);
        cell.CellValue = value;

        var marks = _db.PencilMarks.Where(pm => pm.GameId == game.Id && pm.RowIdx == row && pm.ColIdx == col);
        _db.PencilMarks.RemoveRange(marks);

        game.HintsUsed++;
        _db.HintLogs.Add(new HintLog
        {
            GameId = game.Id,
            RowIdx = row,
            ColIdx = col,
            HintAt = _clock.GetUtcNow().UtcDateTime,
        });

        await _db.SaveChangesAsync(ct);
    }
}
