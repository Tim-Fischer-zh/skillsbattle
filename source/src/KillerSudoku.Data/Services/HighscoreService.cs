using KillerSudoku.Core.Abstractions;
using KillerSudoku.Core.Models;
using KillerSudoku.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.Data.Services;

/// <summary>
/// UC08 — Highscore-Listing. Quelle: <c>vw_Highscore</c> (View über Game ⋈ AppUser ⋈ Puzzle,
/// gefiltert auf <c>IsCompleted = 1</c>). Sortiert nach Score DESC, TimeSeconds ASC als Tie-Breaker.
/// </summary>
public sealed class HighscoreService : IHighscoreService
{
    private readonly SudokuDbContext _db;

    public HighscoreService(SudokuDbContext db) => _db = db;

    public async Task<IReadOnlyList<HighscoreEntry>> GetTopAsync(
        int limit, byte? difficulty = null, CancellationToken ct = default)
    {
        if (limit < 1) return Array.Empty<HighscoreEntry>();
        if (limit > 500) limit = 500;

        // Fallback ohne VIEW: query via Joins (funktioniert auch wenn vw_Highscore noch nicht deployed)
        var q = _db.Games
            .AsNoTracking()
            .Where(g => g.IsCompleted && g.Score != null && g.EndTime != null);
        if (difficulty.HasValue)
            q = q.Where(g => g.Puzzle.Difficulty == difficulty.Value);

        var rows = await q
            .OrderByDescending(g => g.Score)
            .ThenBy(g => g.TimeSeconds)
            .Take(limit)
            .Select(g => new
            {
                Username = g.User.UserName ?? "?",
                g.Puzzle.Difficulty,
                TimeSeconds = g.TimeSeconds ?? 0,
                g.HintsUsed,
                Score = g.Score ?? 0,
                CompletedAt = g.EndTime ?? DateTime.UnixEpoch,
            })
            .ToListAsync(ct);

        return rows
            .Select((r, idx) => new HighscoreEntry(
                idx + 1, r.Username, r.Difficulty, r.TimeSeconds, r.HintsUsed, r.Score, r.CompletedAt))
            .ToList();
    }
}
