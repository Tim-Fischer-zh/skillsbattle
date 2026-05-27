using KillerSudoku.Core.Models;

namespace KillerSudoku.Core.Abstractions;

public interface IAuthService
{
    Task<SavePuzzleResult> RegisterAsync(RegisterDto input, CancellationToken ct = default);
    Task<bool> LoginAsync(LoginDto input, CancellationToken ct = default);
    Task LogoutAsync(int userId, CancellationToken ct = default);
}

public interface IPuzzleService
{
    Task<ValidationResult> ValidateStructureAsync(PuzzleInputDto input, CancellationToken ct = default);
    Task<SavePuzzleResult> SaveIfSolvableAsync(PuzzleInputDto input, int userId, CancellationToken ct = default);
    Task<PagedResult<PuzzleSummary>> ListAsync(byte? difficulty, int page, int pageSize, CancellationToken ct = default);
    Task<PuzzleInputDto?> GetByIdAsync(int puzzleId, CancellationToken ct = default);
}

public interface IGameService
{
    Task<int> StartGameAsync(int userId, int puzzleId, CancellationToken ct = default);
    Task SetCellValueAsync(int gameId, byte row, byte col, byte? value, CancellationToken ct = default);
    Task TogglePencilMarkAsync(int gameId, byte row, byte col, byte markValue, CancellationToken ct = default);
    Task PauseAsync(int gameId, CancellationToken ct = default);
    Task ResumeAsync(int gameId, CancellationToken ct = default);
    Task<CheckResult> CheckSolutionAsync(int gameId, CancellationToken ct = default);
    Task<int> CompleteGameAsync(int gameId, CancellationToken ct = default);
}

public interface IHintService
{
    Task<HintResult> GetHintAsync(int gameId, CancellationToken ct = default);
}

public interface ISolverService
{
    SolveResult Solve(byte[,] givens, IReadOnlyList<CageInputDto> cages);
    int CountSolutions(byte[,] givens, IReadOnlyList<CageInputDto> cages, int limit = 2);
}

public interface IHighscoreService
{
    Task<IReadOnlyList<HighscoreEntry>> GetTopAsync(int limit, byte? difficulty = null, CancellationToken ct = default);
}

public interface IScoreCalculator
{
    int Calculate(int timeSeconds, int hintsUsed);
}

public interface IPuzzleGenerator
{
    /// <summary>
    /// Generates a random, uniquely-solvable Killer-Sudoku puzzle for the given difficulty (1..3).
    /// Throws if generation fails after the configured retry budget.
    /// </summary>
    PuzzleInputDto Generate(byte difficulty, Random? rng = null);
}
