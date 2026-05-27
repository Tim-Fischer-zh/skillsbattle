namespace KillerSudoku.Core.Models;

public sealed record RegisterDto(string Username, string Email, string Password, string PasswordConfirm);
public sealed record LoginDto(string UsernameOrEmail, string Password);

public sealed record CageInputDto(byte Sum, IReadOnlyList<(byte Row, byte Col)> Cells);
public sealed record PuzzleInputDto(byte Difficulty, IReadOnlyList<CageInputDto> Cages);

public sealed record PuzzleSummary(int Id, byte Difficulty, string CreatedBy, DateTime CreatedAt, int? MyBestScore);

public sealed record HighscoreEntry(int Rank, string Username, byte Difficulty, int TimeSeconds, int HintsUsed, int Score, DateTime CompletedAt);

public sealed record SolveResult(int Solutions, byte[,]? Solution);
public sealed record HintResult(byte Row, byte Col, byte Value, HintStrategy Strategy);
public sealed record CheckResult(bool IsCorrect, CheckFailReason? FailReason);

public enum HintStrategy { NakedSingle, CageForced, SolverFallback }
public enum CheckFailReason { SumMismatch, RowDuplicate, ColumnDuplicate, NonetDuplicate, CageSumMismatch, CageDuplicate, Incomplete }

public sealed record ValidationResult(bool IsValid, string? Error);
public sealed record SavePuzzleResult(SaveStatus Status, int? PuzzleId);
public enum SaveStatus { Saved, NotSolvable, MultipleSolutions, InvalidStructure }

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
