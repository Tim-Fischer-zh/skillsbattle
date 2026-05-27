using FluentAssertions;
using KillerSudoku.Data.Entities;
using KillerSudoku.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.IntegrationTests;

/// <summary>
/// HighscoreService Integration-Tests (UC8).
/// </summary>
[Collection(MsSqlCollection.Name)]
public sealed class HighscoreServiceTests
{
    private readonly MsSqlContainerFixture _fx;
    public HighscoreServiceTests(MsSqlContainerFixture fx) => _fx = fx;

    // -------------------------------------------------------------------
    // T072 — Top-N sortiert nach Score DESC.
    //   Seed: 3 abgeschlossene Games mit unterschiedlichen Scores.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T072_GetTop_ReturnsCompletedGamesSortedByScoreDesc()
    {
        var suffix = $"hs_{Guid.NewGuid():N}".Substring(0, 12);
        var userId = await ServiceFactory.CreateUserAsync(_fx, suffix);
        var puzzleId = await CreatePuzzleAsync(userId);

        await InsertCompletedGameAsync(userId, puzzleId, score: 5000, time: 1000, hints: 0);
        await InsertCompletedGameAsync(userId, puzzleId, score: 9000, time: 600,  hints: 1);
        await InsertCompletedGameAsync(userId, puzzleId, score: 7000, time: 800,  hints: 0);

        var top = await ServiceFactory.NewHighscoreService(_fx).GetTopAsync(limit: 10);

        var userGames = top.Where(e => e.Username == $"u_{suffix}").ToList();
        userGames.Should().HaveCount(3);
        userGames.Select(e => e.Score).Should().BeInDescendingOrder();
        userGames[0].Score.Should().Be(9000);
    }

    // -------------------------------------------------------------------
    // T073 — Leere DB (= keine abgeschlossenen Games für Diff 99) → leere Liste.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T073_GetTop_NoCompletedGames_ReturnsEmpty()
    {
        var top = await ServiceFactory.NewHighscoreService(_fx).GetTopAsync(limit: 10, difficulty: 99);
        top.Should().BeEmpty();
    }

    // -------------------------------------------------------------------
    // T070 — Rank-Field beginnt bei 1 und ist monoton aufsteigend.
    // -------------------------------------------------------------------
    [Fact]
    public async Task T070_GetTop_RankStartsAtOneAndIncreases()
    {
        var suffix = $"rk_{Guid.NewGuid():N}".Substring(0, 12);
        var userId = await ServiceFactory.CreateUserAsync(_fx, suffix);
        var puzzleId = await CreatePuzzleAsync(userId);
        await InsertCompletedGameAsync(userId, puzzleId, 1000, 500, 0);
        await InsertCompletedGameAsync(userId, puzzleId, 2000, 400, 0);

        var top = await ServiceFactory.NewHighscoreService(_fx).GetTopAsync(limit: 50);

        top.Should().NotBeEmpty();
        top[0].Rank.Should().Be(1);
        top.Select(e => e.Rank).Should().BeInAscendingOrder();
        top.Select(e => e.Rank).Should().OnlyHaveUniqueItems();
    }

    // -------- helpers --------
    private async Task<int> CreatePuzzleAsync(int userId)
    {
        await using var ctx = ServiceFactory.NewContext(_fx);
        var p = new Puzzle { Difficulty = 1, CreatedById = userId, CreatedAt = DateTime.UtcNow };
        ctx.Puzzles.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task InsertCompletedGameAsync(
        int userId, int puzzleId, int score, int time, int hints)
    {
        await using var ctx = ServiceFactory.NewContext(_fx);
        ctx.Games.Add(new Game
        {
            UserId = userId,
            PuzzleId = puzzleId,
            StartTime = DateTime.UtcNow.AddSeconds(-time),
            EndTime   = DateTime.UtcNow,
            TimeSeconds = time,
            HintsUsed = hints,
            Score = score,
            IsCompleted = true,
        });
        await ctx.SaveChangesAsync();
    }
}
