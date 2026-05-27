using KillerSudoku.Core.Services;
using KillerSudoku.Core.Abstractions;
using KillerSudoku.Core.Models;
using KillerSudoku.Data.Entities;
using KillerSudoku.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.Data.Services;

public sealed class PuzzleService : IPuzzleService
{
    private readonly SudokuDbContext _db;
    private readonly PuzzleStructureValidator _structValidator;
    private readonly ISolverService _solver;

    public PuzzleService(
        SudokuDbContext db,
        PuzzleStructureValidator structValidator,
        ISolverService solver)
    {
        _db = db;
        _structValidator = structValidator;
        _solver = solver;
    }

    public Task<ValidationResult> ValidateStructureAsync(
        PuzzleInputDto input, CancellationToken ct = default)
        => Task.FromResult(_structValidator.Validate(input));

    /// <summary>
    /// UC05 — Saves the puzzle only if (a) structurally valid (incl. Σ=405 fast-fail),
    /// (b) solvable and (c) unique solution. Transactional INSERT into Puzzle+Cage+CageCell.
    /// </summary>
    public async Task<SavePuzzleResult> SaveIfSolvableAsync(
        PuzzleInputDto input, int userId, CancellationToken ct = default)
    {
        var structResult = _structValidator.Validate(input);
        if (!structResult.IsValid)
            return new SavePuzzleResult(SaveStatus.InvalidStructure, null);

        // Solver auf leerem Grid (Puzzle hat keine Givens — die Cages reichen)
        var emptyGrid = new byte[9, 9];
        int solutionCount = _solver.CountSolutions(emptyGrid, input.Cages, limit: 2);
        if (solutionCount == 0)
            return new SavePuzzleResult(SaveStatus.NotSolvable, null);
        if (solutionCount >= 2)
            return new SavePuzzleResult(SaveStatus.MultipleSolutions, null);

        // Atomic save
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var puzzle = new Puzzle
        {
            Difficulty = input.Difficulty,
            CreatedById = userId,
        };
        _db.Puzzles.Add(puzzle);
        await _db.SaveChangesAsync(ct);

        foreach (var cageDto in input.Cages)
        {
            var cage = new Cage
            {
                PuzzleId = puzzle.Id,
                Sum = cageDto.Sum,
            };
            _db.Cages.Add(cage);
            await _db.SaveChangesAsync(ct);

            foreach (var (r, c) in cageDto.Cells)
            {
                _db.CageCells.Add(new CageCell
                {
                    CageId = cage.Id,
                    RowIdx = r,
                    ColIdx = c,
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new SavePuzzleResult(SaveStatus.Saved, puzzle.Id);
    }

    /// <summary>UC12 — Browse / Filter Puzzles. Paginated, neueste zuerst.</summary>
    public async Task<PagedResult<PuzzleSummary>> ListAsync(
        byte? difficulty,
        int page,
        int pageSize,
        int? currentUserId = null,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1)
            return new PagedResult<PuzzleSummary>(Array.Empty<PuzzleSummary>(), 0, page, pageSize);
        if (pageSize > 100) pageSize = 100;

        var q = _db.Puzzles.AsNoTracking();
        if (difficulty.HasValue) q = q.Where(p => p.Difficulty == difficulty.Value);

        int total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PuzzleSummary(
                p.Id,
                p.Difficulty,
                p.CreatedBy.UserName ?? "?",
                p.CreatedAt,
                // UC10 — best (= highest) completed Score for this user/puzzle.
                // Returns null if the user is anonymous OR has never finished it.
                currentUserId == null
                    ? (int?)null
                    : _db.Games
                        .Where(g => g.PuzzleId == p.Id
                                 && g.UserId == currentUserId.Value
                                 && g.IsCompleted
                                 && g.Score != null)
                        .Max(g => (int?)g.Score)))
            .ToListAsync(ct);

        return new PagedResult<PuzzleSummary>(items, total, page, pageSize);
    }

    public async Task<PuzzleInputDto?> GetByIdAsync(int puzzleId, CancellationToken ct = default)
    {
        var puzzle = await _db.Puzzles
            .AsNoTracking()
            .Include(p => p.Cages)
                .ThenInclude(c => c.Cells)
            .FirstOrDefaultAsync(p => p.Id == puzzleId, ct);

        if (puzzle is null) return null;

        var cages = puzzle.Cages
            .Select(c => new CageInputDto(
                c.Sum,
                c.Cells.Select(cc => (cc.RowIdx, cc.ColIdx))
                    .OrderBy(t => t.RowIdx).ThenBy(t => t.ColIdx)
                    .ToList()))
            .ToList();

        return new PuzzleInputDto(puzzle.Difficulty, cages);
    }
}
