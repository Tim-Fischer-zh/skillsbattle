namespace KillerSudoku.Core.Models;

// Puzzle structure (used for both Editor input and Solver verification)
public sealed record CageInputDto(byte Sum, IReadOnlyList<(byte Row, byte Col)> Cells);

/// <summary>
/// Optional clue cell — pre-filled value that the player sees at game start.
/// Not persisted at the Puzzle level (Spec UC04 "No solution is recorded"); the
/// generator computes them deterministically from the cage layout so the editor
/// preview and the game start show identical prefills.
/// </summary>
public sealed record ClueDto(byte Row, byte Col, byte Value);

public sealed record PuzzleInputDto(
    byte Difficulty,
    IReadOnlyList<CageInputDto> Cages,
    IReadOnlyList<ClueDto>? Clues = null);

// List projection (UC12 Browse). `BestScore` is the requesting user's best score for
// this puzzle (null = never played or no completed game).
public sealed record PuzzleSummary(
    int Id,
    byte Difficulty,
    string CreatedBy,
    DateTime CreatedAt,
    int? MyBestScore);

// Highscore row (UC08)
public sealed record HighscoreEntry(
    int Rank,
    string Username,
    byte Difficulty,
    int TimeSeconds,
    int HintsUsed,
    int Score,
    DateTime CompletedAt);

// Solver / Hint / Check results (UC07/UC09/UC11)
public sealed record SolveResult(int Solutions, byte[,]? Solution);
public sealed record HintResult(byte Row, byte Col, byte Value, HintStrategy Strategy);
public sealed record CheckResult(bool IsCorrect, CheckFailReason? FailReason);

public enum HintStrategy { NakedSingle, CageForced, SolverFallback }
public enum CheckFailReason { SumMismatch, RowDuplicate, ColumnDuplicate, NonetDuplicate, CageSumMismatch, CageDuplicate, Incomplete }

// Validation outcomes (UC04/UC05)
public sealed record ValidationResult(bool IsValid, string? Error);
public sealed record SavePuzzleResult(SaveStatus Status, int? PuzzleId);
public enum SaveStatus { Saved, NotSolvable, MultipleSolutions, InvalidStructure }

// Generic paged-list wrapper
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
