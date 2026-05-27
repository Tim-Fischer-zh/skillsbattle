# Funktionalitäts-Matrix

Mappt jeden Use Case auf Screens (Mockup-Anker), Blazor-Components, Backend-Service-Methoden und DB-Operationen.
Diese Matrix ist die Brücke zwischen `use-cases.md`, `docs/mockup-briefs.md` und `docs/test-protocol`.

## Stack-Konventionen

- **Service-Layer:** `IXxxService` Interface + `XxxService` Implementation, DI-registered
- **DB-Layer:** Entity Framework Core 10 (`SudokuDbContext`) ODER Dapper-Repositories — Wahl beeinflusst nicht das Interface
- **Blazor:** Server-Side Components, `@page` directives für Routes, SignalR-Verbindung permanent
- **Auth:** ASP.NET Core Identity (Cookie-basiert)

## Screen-Inventar

| Screen-ID | Route | Auth | Beschreibung |
|-----------|-------|------|--------------|
| S1 — Home/Rules | `/` | public | Regeln + Beispiel-Sudoku-Mini |
| S2 — Register | `/register` | public | Registrierung |
| S3 — Login | `/login` | public | Login-Formular |
| S4 — PuzzleList | `/puzzles` | required | Browse + Filter (UC12) |
| S5 — EnterPuzzle | `/puzzles/new` | required | Puzzle-Editor (UC04+UC05) |
| S6 — PlayPuzzle | `/puzzles/{id}/play` | required | Spiel-Screen (UC06–UC09, UC13, UC14) |
| S7 — Highscore | `/highscore` | required | Top-N Liste (UC08) |
| S8 — Layout | * | * | Navigation, User-Indicator, Logout |

## Matrix

| UC | Screen | Blazor-Component | Service-Methode | DB-Operation |
|----|--------|------------------|------------------|--------------|
| UC01 Read Rules | S1 | `Home.razor`, `RulesPanel.razor`, `MiniSudokuExample.razor` | — | — |
| UC02 Create User | S2 | `Register.razor`, `RegisterForm.razor` | `UserManager<AppUser>.CreateAsync + SignInManager<AppUser>.SignInAsync` | INSERT INTO AppUser |
| UC03 Login | S3 | `Login.razor`, `LoginForm.razor` | `SignInManager<AppUser>.PasswordSignInAsync (mit Lockout)` | SELECT/UPDATE AppUser via Identity; SET Cookie |
| UC04 Enter Puzzle | S5 | `EnterPuzzle.razor`, `PuzzleGrid.razor`, `CageEditor.razor` | `IPuzzleService.ValidateStructureAsync(PuzzleInputDto)` | — (in-memory validation) |
| UC05 Save New Puzzle | S5 | `EnterPuzzle.razor` (Save-Button-Handler) | `IPuzzleService.SaveIfSolvableAsync(PuzzleInputDto)` → `ISolverService.CountSolutions(PuzzleInputDto)` | INSERT INTO Puzzle, Cage, CageCell (nur bei Solutions==1) |
| UC06 Solve Puzzle | S6 | `PlayPuzzle.razor`, `PuzzleGrid.razor`, `Toolbar.razor` | `IGameService.StartGameAsync(userId, puzzleId)` | INSERT INTO Game (StartTime); SELECT Cages |
| UC07 Ask for Hint | S6 (Hint-Button) | `HintButton.razor` | `IHintService.GetHintAsync(gameId)` (verwendet `ISolverService.Solve`) | UPDATE Game SET HintsUsed +=1; INSERT HintLog; UPDATE GameCell |
| UC08 Show High Score | S7 | `Highscore.razor`, `HighscoreTable.razor` | `IHighscoreService.GetTopAsync(limit)` | LINQ-Join über Games ⋈ AppUser ⋈ Puzzle WHERE IsCompleted=1 (vw_Highscore existiert im Schema, wird vom Service aktuell nicht verwendet) |
| UC09 Check Solution | S6 (Check-Button + auto) | `CheckSolutionButton.razor` | `IGameService.CheckSolutionAsync(gameId)` → 1) Sum-Check 405, 2) Row/Col/Nonet, 3) Cage-Check | SELECT GameCell; (kein UPDATE) |
| UC10 Save Result | S6 (auto nach UC09 OK) | — (Service-side) | `IGameService.CompleteGameAsync(gameId)` | UPDATE Game SET EndTime, TimeSeconds, Score, IsCompleted=1 |
| UC11 Auto Solve | (intern) | — (intern, ggf. Debug-Button) | `ISolverService.Solve(puzzle): {Solutions, Solution?}` | — (read CageCell) |
| UC12 Browse/Filter | S4 | `PuzzleList.razor`, `FilterBar.razor`, `PuzzleCard.razor` | `IPuzzleService.ListAsync(difficulty?, page, pageSize)` | SELECT Puzzle WHERE Difficulty = @d ORDER BY CreatedAt; COUNT(*) |
| UC13 Pause/Resume | S6 (Pause-Button) | `PauseButton.razor` | `IGameService.PauseAsync(gameId)` / `ResumeAsync(gameId)` | UPDATE Game SET IsPaused, PausedAt, TotalPausedSeconds |
| UC14 Pencil Marks | S6 (Pencil-Toggle) | `PencilMarkLayer.razor` (innerhalb `PuzzleGrid`) | `IGameService.TogglePencilMarkAsync(gameId, row, col, value)` | INSERT/DELETE PencilMark |

## Service-Interfaces (komplett, für Unit-Tests)

```text
// Es gibt keinen eigenen IAuthService.
// UC02 (Register) und UC03 (Login) verwenden direkt ASP.NET Core Identity:
//   • UserManager<AppUser>   — CreateAsync, FindByNameAsync, GeneratePasswordHash
//   • SignInManager<AppUser> — PasswordSignInAsync, SignOutAsync
// Lockout-Konfiguration (5 Fehlversuche / 5 min) in Program.cs konfiguriert.
// Logout: POST /logout-Endpoint in Program.cs registriert (siehe AC03.x / V03).
```

```csharp
public interface IPuzzleService {
    Task<ValidationResult>           ValidateStructureAsync(PuzzleInputDto input, CancellationToken ct = default);
    Task<SavePuzzleResult>           SaveIfSolvableAsync(PuzzleInputDto input, int userId, CancellationToken ct = default);
    Task<PagedResult<PuzzleSummary>> ListAsync(byte? difficulty, int page, int pageSize, int? currentUserId = null, CancellationToken ct = default);
    Task<PuzzleInputDto?>            GetByIdAsync(int puzzleId, CancellationToken ct = default);
}

public interface IGameService {
    Task<int>         StartGameAsync(int userId, int puzzleId, CancellationToken ct = default);
    Task              SetCellValueAsync(int gameId, byte row, byte col, byte? value, CancellationToken ct = default);
    Task              TogglePencilMarkAsync(int gameId, byte row, byte col, byte markValue, CancellationToken ct = default);
    Task              PauseAsync(int gameId, CancellationToken ct = default);
    Task              ResumeAsync(int gameId, CancellationToken ct = default);
    Task<CheckResult> CheckSolutionAsync(int gameId, CancellationToken ct = default);   // sum-check first
    Task<int>         CompleteGameAsync(int gameId, CancellationToken ct = default);    // returns Score
}

public interface IHintService {
    Task<HintResult> GetHintAsync(int gameId, CancellationToken ct = default);
}

public interface ISolverService {
    // Returns 0 (unsolvable), 1 (unique), or limit (≥ limit — stops at 2 by default).
    SolveResult Solve(byte[,] givens, IReadOnlyList<CageInputDto> cages);
    int CountSolutions(byte[,] givens, IReadOnlyList<CageInputDto> cages, int limit = 2);
}

public interface IHighscoreService {
    Task<IReadOnlyList<HighscoreEntry>> GetTopAsync(int limit, byte? difficulty = null, CancellationToken ct = default);
}

public interface IScoreCalculator {
    int Calculate(int timeSeconds, int hintsUsed);
}

public interface IPuzzleGenerator {
    PuzzleInputDto Generate(byte difficulty, Random? rng = null);
}
```

## DTOs / Records (Kurz-Inventar)

| Type | Felder | Zweck |
|------|--------|-------|
| `PuzzleInputDto` | Difficulty, Cages (List<CageInputDto>) | UC04/UC05 Input |
| `CageInputDto` | Sum, Cells (List<(Row, Col)>) | Cage-Definition |
| `PuzzleSummary` | Id, byte Difficulty, CreatedBy, CreatedAt, int? MyBestScore | UC12 Output |
| `CheckResult` | IsCorrect, FirstViolation? | UC09 Output |
| `HintResult` | RowIdx, ColIdx, Value | UC07 Output |
| `SolveResult` | Solutions (0\|1\|2), Solution? (int[,]) | UC11 Output |
| `HighscoreEntry` | Rank, Username, byte Difficulty, TimeSeconds, HintsUsed, Score, int GameId | UC08 Output |
| `PuzzleInputDto?` | (siehe oben) | UC04/UC12 Output (Editor-Reload) |
| `PagedResult<T>` | Items, TotalCount, Page, PageSize, TotalPages | Pagination-Wrapper |
| `ValidationResult` / `SavePuzzleResult` | IsValid/Error; SaveStatus enum: Saved, NotSolvable, MultipleSolutions, InvalidStructure | UC04/UC05 Output |

## Kritische Abhängigkeiten (für Test-Order)

```
ISolverService    ← Pure Logic (Unit-Test ZUERST)
IScoreCalculator ← GameService.CompleteGameAsync (UC10) — pure Funktion, ZUERST testen wie der Solver.
  ↑
IPuzzleService.SaveIfSolvableAsync (UC05)
IGameService.CheckSolutionAsync   (UC09)
IHintService.GetHintAsync         (UC07)
```

→ Solver-Tests sind **kritisch**: 5+ Unit-Tests (Beispiele aus README, Edge-Cases, Unsolvable, Multi-Solution).
