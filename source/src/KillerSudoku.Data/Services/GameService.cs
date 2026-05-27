using KillerSudoku.Core.Services;
using KillerSudoku.Core.Abstractions;
using KillerSudoku.Core.Models;
using KillerSudoku.Data.Entities;
using KillerSudoku.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.Data.Services;

public sealed class GameService : IGameService
{
    private readonly SudokuDbContext _db;
    private readonly SolutionValidator _validator;
    private readonly IScoreCalculator _score;
    private readonly TimeProvider _clock;

    public GameService(
        SudokuDbContext db,
        SolutionValidator validator,
        IScoreCalculator score,
        TimeProvider? clock = null)
    {
        _db = db;
        _validator = validator;
        _score = score;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// UC06 — Start a new game OR resume the active one for (user, puzzle).
    /// Single-active-game-per-pair is enforced by filtered unique index UX_Game_ActiveOnly.
    /// </summary>
    public async Task<int> StartGameAsync(int userId, int puzzleId, CancellationToken ct = default)
    {
        var existing = await _db.Games
            .Where(g => g.UserId == userId && g.PuzzleId == puzzleId && !g.IsCompleted)
            .Select(g => (int?)g.Id)
            .FirstOrDefaultAsync(ct);

        if (existing.HasValue) return existing.Value;

        var puzzleExists = await _db.Puzzles.AnyAsync(p => p.Id == puzzleId, ct);
        if (!puzzleExists) throw new InvalidOperationException($"Puzzle {puzzleId} nicht gefunden");

        var game = new Game
        {
            UserId = userId,
            PuzzleId = puzzleId,
            StartTime = _clock.GetUtcNow().UtcDateTime,
        };
        _db.Games.Add(game);
        await _db.SaveChangesAsync(ct);

        // Initialisiere 81 GameCells (alle leer)
        for (byte r = 0; r < 9; r++)
        for (byte c = 0; c < 9; c++)
            _db.GameCells.Add(new GameCell
            {
                GameId = game.Id,
                RowIdx = r,
                ColIdx = c,
                CellValue = null,
            });
        await _db.SaveChangesAsync(ct);

        return game.Id;
    }

    public async Task SetCellValueAsync(
        int gameId, byte row, byte col, byte? value, CancellationToken ct = default)
    {
        if (row > 8 || col > 8) throw new ArgumentOutOfRangeException(nameof(row));
        if (value is not null && (value < 1 || value > 9))
            throw new ArgumentOutOfRangeException(nameof(value), "CellValue muss 1..9 oder null sein");

        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct)
            ?? throw new InvalidOperationException($"Game {gameId} nicht gefunden");
        if (game.IsCompleted) throw new InvalidOperationException("Game ist bereits abgeschlossen");

        var cell = await _db.GameCells
            .FirstOrDefaultAsync(gc => gc.GameId == gameId && gc.RowIdx == row && gc.ColIdx == col, ct)
            ?? throw new InvalidOperationException($"GameCell ({row},{col}) nicht gefunden");

        cell.CellValue = value;

        // UC14 AC14.2: Beim Setzen eines finalen Wertes alle Pencil-Marks der Zelle entfernen
        if (value.HasValue)
        {
            var marks = _db.PencilMarks.Where(pm => pm.GameId == gameId && pm.RowIdx == row && pm.ColIdx == col);
            _db.PencilMarks.RemoveRange(marks);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC14 — Toggle a single pencil mark in/out.</summary>
    public async Task TogglePencilMarkAsync(
        int gameId, byte row, byte col, byte markValue, CancellationToken ct = default)
    {
        if (markValue < 1 || markValue > 9)
            throw new ArgumentOutOfRangeException(nameof(markValue));

        var cell = await _db.GameCells
            .FirstOrDefaultAsync(gc => gc.GameId == gameId && gc.RowIdx == row && gc.ColIdx == col, ct)
            ?? throw new InvalidOperationException("GameCell nicht gefunden");

        if (cell.CellValue.HasValue)
            throw new InvalidOperationException(
                "Pencil-Mark nicht erlaubt in Zelle mit finalem Wert (V13)");

        var existing = await _db.PencilMarks.FirstOrDefaultAsync(
            pm => pm.GameId == gameId && pm.RowIdx == row && pm.ColIdx == col && pm.MarkValue == markValue, ct);
        if (existing is null)
        {
            _db.PencilMarks.Add(new PencilMark
            {
                GameId = gameId, RowIdx = row, ColIdx = col, MarkValue = markValue
            });
        }
        else
        {
            _db.PencilMarks.Remove(existing);
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC13 — Pause: TotalPausedSeconds wird beim Resume akkumuliert.</summary>
    public async Task PauseAsync(int gameId, CancellationToken ct = default)
    {
        var game = await GetActiveGameAsync(gameId, ct);
        if (game.IsPaused) return;
        game.IsPaused = true;
        game.PausedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResumeAsync(int gameId, CancellationToken ct = default)
    {
        var game = await GetActiveGameAsync(gameId, ct);
        if (!game.IsPaused) return;
        if (game.PausedAt is null) throw new InvalidOperationException("Inkonsistenter Pause-State");

        var pausedSeconds = (int)Math.Max(0,
            (_clock.GetUtcNow().UtcDateTime - game.PausedAt.Value).TotalSeconds);
        game.TotalPausedSeconds += pausedSeconds;
        game.IsPaused = false;
        game.PausedAt = null;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC09 — Check Solution. Fast-Fail Sum 405 → Full Validation.</summary>
    public async Task<CheckResult> CheckSolutionAsync(int gameId, CancellationToken ct = default)
    {
        var game = await _db.Games
            .Include(g => g.Cells)
            .Include(g => g.Puzzle).ThenInclude(p => p.Cages).ThenInclude(c => c.Cells)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct)
            ?? throw new InvalidOperationException("Game nicht gefunden");

        var grid = new byte[9, 9];
        foreach (var gc in game.Cells)
        {
            if (!gc.CellValue.HasValue)
                return new CheckResult(false, CheckFailReason.Incomplete);
            grid[gc.RowIdx, gc.ColIdx] = gc.CellValue.Value;
        }

        var cages = game.Puzzle.Cages
            .Select(c => new CageInputDto(
                c.Sum,
                c.Cells.Select(cc => (cc.RowIdx, cc.ColIdx)).ToList()))
            .ToList();

        return _validator.Validate(grid, cages);
    }

    /// <summary>UC10 — Save Result. Score = MAX(0, 10000 − TimeSeconds − HintsUsed × 300).</summary>
    public async Task<int> CompleteGameAsync(int gameId, CancellationToken ct = default)
    {
        var game = await GetActiveGameAsync(gameId, ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        int rawSeconds = (int)(now - game.StartTime).TotalSeconds;
        int timeSeconds = Math.Max(0, rawSeconds - game.TotalPausedSeconds);
        int score = _score.Calculate(timeSeconds, game.HintsUsed);

        game.EndTime = now;
        game.TimeSeconds = timeSeconds;
        game.Score = score;
        game.IsCompleted = true;
        await _db.SaveChangesAsync(ct);

        return score;
    }

    private async Task<Game> GetActiveGameAsync(int gameId, CancellationToken ct)
    {
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct)
            ?? throw new InvalidOperationException("Game nicht gefunden");
        if (game.IsCompleted) throw new InvalidOperationException("Game ist abgeschlossen");
        return game;
    }
}
