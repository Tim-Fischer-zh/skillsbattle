using FluentAssertions;
using KillerSudoku.Data.Entities;
using KillerSudoku.Data.Persistence;
using KillerSudoku.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.IntegrationTests;

/// <summary>
/// DB-Schema-/Constraint-Tests gegen einen frisch migrierten MS-SQL-Container.
/// Quelle der erwarteten Constraints: db/sudoku.sql + EF-Migration
/// 20260527103715_Initial.cs (siehe SudokuDbContext.OnModelCreating).
///
/// Test-IDs entsprechen docs/test-protocol.md.
/// </summary>
[Collection(MsSqlCollection.Name)]
public sealed class DbConstraintTests
{
    private readonly MsSqlContainerFixture _fx;

    public DbConstraintTests(MsSqlContainerFixture fx) => _fx = fx;

    private SudokuDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<SudokuDbContext>()
            .UseSqlServer(_fx.ConnectionString)
            .Options;
        return new SudokuDbContext(options);
    }

    /// <summary>
    /// Legt einen frischen AppUser an und gibt seine Id zurück. Jeder Test
    /// nutzt einen einzigartigen Username/Email damit kein UNIQUE-Conflict
    /// die eigentliche Assertion verschluckt.
    /// </summary>
    private async Task<int> CreateUserAsync(string suffix)
    {
        await using var ctx = NewContext();
        var user = new AppUser
        {
            UserName = $"user_{suffix}",
            NormalizedUserName = $"USER_{suffix}".ToUpperInvariant(),
            Email = $"user_{suffix}@test.ch",
            NormalizedEmail = $"USER_{suffix}@TEST.CH".ToUpperInvariant(),
            EmailConfirmed = true,
            PasswordHash = "fake-hash",
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    private async Task<int> CreatePuzzleAsync(int userId, byte difficulty = 1)
    {
        await using var ctx = NewContext();
        var p = new Puzzle { Difficulty = difficulty, CreatedById = userId, CreatedAt = DateTime.UtcNow };
        ctx.Puzzles.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    // ---------------------------------------------------------------------
    // T036/T037 — Puzzle-Tabelle hat KEINE Solution-Spalte
    //   README §1.3 + UC04 AC04.4: "No solution is recorded"
    // ---------------------------------------------------------------------
    [Fact]
    public async Task T037_Puzzle_HasNoSolutionColumn()
    {
        await using var ctx = NewContext();
        var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = 'Puzzle' AND COLUMN_NAME LIKE '%Solution%'";
        var count = (int)(await cmd.ExecuteScalarAsync())!;

        count.Should().Be(0, "Puzzle darf KEINE Solution-Spalte haben (UC04).");
    }

    // ---------------------------------------------------------------------
    // T040 — Trigger trg_CageCell_UniquePerPuzzle:
    //   Eine Zelle (RowIdx, ColIdx) darf pro Puzzle nur in einem Cage liegen.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task T040_Trigger_BlocksDoubleAssignmentOfCellInSamePuzzle()
    {
        var userId = await CreateUserAsync("t040");
        var puzzleId = await CreatePuzzleAsync(userId);

        // Cage 1 mit Zelle (0,0) — soll OK gehen
        int cage1Id, cage2Id;
        await using (var ctx = NewContext())
        {
            var cage1 = new Cage { PuzzleId = puzzleId, Sum = 10 };
            ctx.Cages.Add(cage1);
            await ctx.SaveChangesAsync();
            cage1Id = cage1.Id;

            ctx.CageCells.Add(new CageCell { CageId = cage1Id, RowIdx = 0, ColIdx = 0 });
            await ctx.SaveChangesAsync();
        }

        // Cage 2 im gleichen Puzzle — versucht die GLEICHE Zelle (0,0)
        await using (var ctx = NewContext())
        {
            var cage2 = new Cage { PuzzleId = puzzleId, Sum = 5 };
            ctx.Cages.Add(cage2);
            await ctx.SaveChangesAsync();
            cage2Id = cage2.Id;
        }

        // Insert der konfligierenden CageCell → Trigger feuert + ROLLBACK
        var act = async () =>
        {
            await using var ctx = NewContext();
            ctx.CageCells.Add(new CageCell { CageId = cage2Id, RowIdx = 0, ColIdx = 0 });
            await ctx.SaveChangesAsync();
        };

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("CageCell", "Trigger trg_CageCell_UniquePerPuzzle muss greifen.");
    }

    // ---------------------------------------------------------------------
    // T057 — CK_GameCell_Value: CellValue BETWEEN 1 AND 9 oder NULL.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task T057_GameCell_CellValue_OutOfRange_IsRejected()
    {
        var userId = await CreateUserAsync("t057");
        var puzzleId = await CreatePuzzleAsync(userId);

        int gameId;
        await using (var ctx = NewContext())
        {
            var game = new Game { UserId = userId, PuzzleId = puzzleId, StartTime = DateTime.UtcNow };
            ctx.Games.Add(game);
            await ctx.SaveChangesAsync();
            gameId = game.Id;
        }

        // CellValue=10 verletzt CK_GameCell_Value
        var act = async () =>
        {
            await using var ctx = NewContext();
            ctx.GameCells.Add(new GameCell { GameId = gameId, RowIdx = 0, ColIdx = 0, CellValue = 10 });
            await ctx.SaveChangesAsync();
        };

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("CK_GameCell_Value");
    }

    // ---------------------------------------------------------------------
    // T087 — CK_Game_TimeSeconds: TimeSeconds IS NULL OR TimeSeconds >= 0.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task T087_Game_TimeSeconds_Negative_IsRejected()
    {
        var userId = await CreateUserAsync("t087");
        var puzzleId = await CreatePuzzleAsync(userId);

        var act = async () =>
        {
            await using var ctx = NewContext();
            ctx.Games.Add(new Game
            {
                UserId = userId,
                PuzzleId = puzzleId,
                StartTime = DateTime.UtcNow,
                TimeSeconds = -1,
                HintsUsed = 0,
                TotalPausedSeconds = 0,
            });
            await ctx.SaveChangesAsync();
        };

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("CK_Game_TimeSeconds");
    }

    // ---------------------------------------------------------------------
    // T088 — CK_Game_HintsUsed: HintsUsed >= 0.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task T088_Game_HintsUsed_Negative_IsRejected()
    {
        var userId = await CreateUserAsync("t088");
        var puzzleId = await CreatePuzzleAsync(userId);

        var act = async () =>
        {
            await using var ctx = NewContext();
            ctx.Games.Add(new Game
            {
                UserId = userId,
                PuzzleId = puzzleId,
                StartTime = DateTime.UtcNow,
                HintsUsed = -1,
                TotalPausedSeconds = 0,
            });
            await ctx.SaveChangesAsync();
        };

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("CK_Game_HintsUsed");
    }

    // ---------------------------------------------------------------------
    // T109 — Filtered Unique Index UX_Game_ActiveOnly:
    //   Pro (UserId, PuzzleId) max. 1 Row mit IsCompleted = 0.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task T109_FilteredUniqueIndex_BlocksSecondActiveGame()
    {
        var userId = await CreateUserAsync("t109");
        var puzzleId = await CreatePuzzleAsync(userId);

        // Erstes aktives Game — OK
        await using (var ctx = NewContext())
        {
            ctx.Games.Add(new Game
            {
                UserId = userId,
                PuzzleId = puzzleId,
                StartTime = DateTime.UtcNow,
                HintsUsed = 0,
                TotalPausedSeconds = 0,
                IsCompleted = false,
            });
            await ctx.SaveChangesAsync();
        }

        // Zweites aktives Game für gleiche (User, Puzzle) — Filter trifft → Unique-Verletzung
        var act = async () =>
        {
            await using var ctx = NewContext();
            ctx.Games.Add(new Game
            {
                UserId = userId,
                PuzzleId = puzzleId,
                StartTime = DateTime.UtcNow,
                HintsUsed = 0,
                TotalPausedSeconds = 0,
                IsCompleted = false,
            });
            await ctx.SaveChangesAsync();
        };

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("UX_Game_ActiveOnly");
    }

    // ---------------------------------------------------------------------
    // T118 — CK_PencilMark_Value: MarkValue BETWEEN 1 AND 9.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task T118_PencilMark_MarkValue_OutOfRange_IsRejected()
    {
        var userId = await CreateUserAsync("t118");
        var puzzleId = await CreatePuzzleAsync(userId);

        int gameId;
        await using (var ctx = NewContext())
        {
            var game = new Game
            {
                UserId = userId,
                PuzzleId = puzzleId,
                StartTime = DateTime.UtcNow,
                HintsUsed = 0,
                TotalPausedSeconds = 0,
            };
            ctx.Games.Add(game);
            await ctx.SaveChangesAsync();
            gameId = game.Id;
        }

        var act = async () =>
        {
            await using var ctx = NewContext();
            ctx.PencilMarks.Add(new PencilMark { GameId = gameId, RowIdx = 0, ColIdx = 0, MarkValue = 10 });
            await ctx.SaveChangesAsync();
        };

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("CK_PencilMark_Value");
    }

    // ---------------------------------------------------------------------
    // Bonus: CK_Puzzle_Difficulty — Difficulty BETWEEN 1 AND 3.
    //   Deckt T037 (Test-Protokoll-Variante) zusätzlich ab.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Puzzle_Difficulty_OutOfRange_IsRejected()
    {
        var userId = await CreateUserAsync("difficulty");

        var act = async () =>
        {
            await using var ctx = NewContext();
            ctx.Puzzles.Add(new Puzzle { Difficulty = 4, CreatedById = userId, CreatedAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync();
        };

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("CK_Puzzle_Difficulty");
    }
}
