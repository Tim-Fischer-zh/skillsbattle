using KillerSudoku.Core.Abstractions;
using KillerSudoku.Data.Entities;
using KillerSudoku.Data.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KillerSudoku.Data.Services;

/// <summary>
/// Seeds N random, uniquely-solvable Killer-Sudoku puzzles per difficulty level
/// into the database, owned by a synthetic "system" user.
/// </summary>
public sealed class PuzzleSeeder
{
    private const string SystemUserName = "system";
    private const string SystemUserEmail = "system@killersudoku.local";

    private readonly SudokuDbContext _db;
    private readonly IPuzzleGenerator _generator;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<PuzzleSeeder> _log;

    public PuzzleSeeder(
        SudokuDbContext db,
        IPuzzleGenerator generator,
        UserManager<AppUser> userManager,
        ILogger<PuzzleSeeder> log)
    {
        _db = db;
        _generator = generator;
        _userManager = userManager;
        _log = log;
    }

    /// <summary>
    /// Creates <paramref name="countPerDifficulty"/> puzzles for each difficulty (1, 2, 3).
    /// Existing system-owned puzzles are not removed; the seeder appends.
    /// </summary>
    public async Task<int> SeedAsync(int countPerDifficulty, CancellationToken ct = default)
    {
        if (countPerDifficulty < 1)
            throw new ArgumentOutOfRangeException(nameof(countPerDifficulty));

        int systemUserId = await EnsureSystemUserAsync(ct);
        int created = 0;

        for (byte difficulty = 1; difficulty <= 3; difficulty++)
        {
            for (int i = 0; i < countPerDifficulty; i++)
            {
                ct.ThrowIfCancellationRequested();

                var input = _generator.Generate(difficulty);

                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                var puzzle = new Puzzle
                {
                    Difficulty = difficulty,
                    CreatedById = systemUserId,
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
                created++;
                _log.LogInformation(
                    "Seeded puzzle #{PuzzleId} (difficulty {Difficulty}, {CageCount} cages)",
                    puzzle.Id, difficulty, input.Cages.Count);
            }
        }

        return created;
    }

    private async Task<int> EnsureSystemUserAsync(CancellationToken ct)
    {
        var existing = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == SystemUserName, ct);
        if (existing != null) return existing.Id;

        var user = new AppUser
        {
            UserName = SystemUserName,
            Email = SystemUserEmail,
            EmailConfirmed = true,
            LockoutEnabled = false,
        };

        // Random strong password that is intentionally not persisted anywhere readable —
        // the system user is meant to own seeded puzzles, never to sign in.
        var randomBytes = new byte[24];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        string password = "Sys!" + Convert.ToBase64String(randomBytes);

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new InvalidOperationException($"Failed to create system user: {errors}");
        }

        return user.Id;
    }
}
