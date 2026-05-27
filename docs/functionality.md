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
| UC02 Create User | S2 | `Register.razor`, `RegisterForm.razor` | `IAuthService.RegisterAsync(RegisterDto)` | INSERT INTO AppUser |
| UC03 Login | S3 | `Login.razor`, `LoginForm.razor` | `IAuthService.LoginAsync(LoginDto)` | SELECT PasswordHash; SET Cookie |
| UC04 Enter Puzzle | S5 | `EnterPuzzle.razor`, `PuzzleGrid.razor`, `CageEditor.razor` | `IPuzzleService.ValidateStructureAsync(PuzzleInputDto)` | — (in-memory validation) |
| UC05 Save New Puzzle | S5 | `EnterPuzzle.razor` (Save-Button-Handler) | `IPuzzleService.SaveIfSolvableAsync(PuzzleInputDto)` → `ISolverService.CountSolutions(PuzzleInputDto)` | INSERT INTO Puzzle, Cage, CageCell (nur bei Solutions==1) |
| UC06 Solve Puzzle | S6 | `PlayPuzzle.razor`, `PuzzleGrid.razor`, `Toolbar.razor` | `IGameService.StartGameAsync(userId, puzzleId)` | INSERT INTO Game (StartTime); SELECT Cages |
| UC07 Ask for Hint | S6 (Hint-Button) | `HintButton.razor` | `IHintService.GetHintAsync(gameId)` (verwendet `ISolverService.Solve`) | UPDATE Game SET HintsUsed +=1; INSERT HintLog; UPDATE GameCell |
| UC08 Show High Score | S7 | `Highscore.razor`, `HighscoreTable.razor` | `IHighscoreService.GetTopAsync(limit)` | SELECT FROM vw_Highscore ORDER BY Score DESC |
| UC09 Check Solution | S6 (Check-Button + auto) | `CheckSolutionButton.razor` | `IGameService.CheckSolutionAsync(gameId)` → 1) Sum-Check 405, 2) Row/Col/Nonet, 3) Cage-Check | SELECT GameCell; (kein UPDATE) |
| UC10 Save Result | S6 (auto nach UC09 OK) | — (Service-side) | `IGameService.CompleteGameAsync(gameId)` | UPDATE Game SET EndTime, TimeSeconds, Score, IsCompleted=1 |
| UC11 Auto Solve | (intern) | — (intern, ggf. Debug-Button) | `ISolverService.Solve(puzzle): {Solutions, Solution?}` | — (read CageCell) |
| UC12 Browse/Filter | S4 | `PuzzleList.razor`, `FilterBar.razor`, `PuzzleCard.razor` | `IPuzzleService.ListAsync(difficulty?, page, pageSize)` | SELECT Puzzle WHERE Difficulty = @d ORDER BY CreatedAt; COUNT(*) |
| UC13 Pause/Resume | S6 (Pause-Button) | `PauseButton.razor` | `IGameService.PauseAsync(gameId)` / `ResumeAsync(gameId)` | UPDATE Game SET IsPaused, PausedAt, TotalPausedSeconds |
| UC14 Pencil Marks | S6 (Pencil-Toggle) | `PencilMarkLayer.razor` (innerhalb `PuzzleGrid`) | `IGameService.TogglePencilMarkAsync(gameId, row, col, value)` | INSERT/DELETE PencilMark |

## Service-Interfaces (komplett, für Unit-Tests)

```csharp
public interface IAuthService {
    Task<RegisterResult> RegisterAsync(RegisterDto input);
    Task<LoginResult>    LoginAsync(LoginDto input);
    Task                 LogoutAsync(int userId);
}

public interface IPuzzleService {
    Task<ValidationResult>  ValidateStructureAsync(PuzzleInputDto input);
    Task<SavePuzzleResult>  SaveIfSolvableAsync(PuzzleInputDto input, int userId);
    Task<PageResult<PuzzleListItem>> ListAsync(int? difficulty, int page, int pageSize);
    Task<PuzzleDto?>        GetByIdAsync(int puzzleId);
}

public interface IGameService {
    Task<int>            StartGameAsync(int userId, int puzzleId);
    Task                 SetCellValueAsync(int gameId, int row, int col, int? value);
    Task                 TogglePencilMarkAsync(int gameId, int row, int col, int markValue);
    Task                 PauseAsync(int gameId);
    Task                 ResumeAsync(int gameId);
    Task<CheckResult>    CheckSolutionAsync(int gameId);   // sum-check first
    Task<int>            CompleteGameAsync(int gameId);    // returns Score
}

public interface IHintService {
    Task<HintResult> GetHintAsync(int gameId);
}

public interface ISolverService {
    // Returns 0 (unsolvable), 1 (unique), or 2 (multiple — stops at 2 for performance)
    SolveResult Solve(int[,] givenValues, IReadOnlyList<CageDef> cages);
    int CountSolutions(int[,] givenValues, IReadOnlyList<CageDef> cages);
}

public interface IHighscoreService {
    Task<IReadOnlyList<HighscoreEntry>> GetTopAsync(int limit);
}
```

## DTOs / Records (Kurz-Inventar)

| Type | Felder | Zweck |
|------|--------|-------|
| `RegisterDto` | Username, Email, Password, PasswordConfirm | UC02 Input |
| `LoginDto` | UsernameOrEmail, Password | UC03 Input |
| `PuzzleInputDto` | Difficulty, Cages (List<CageInputDto>) | UC04/UC05 Input |
| `CageInputDto` | Sum, Cells (List<(Row, Col)>) | Cage-Definition |
| `PuzzleListItem` | Id, Difficulty, CreatedBy, CreatedAt, MyBestScore | UC12 Output |
| `CheckResult` | IsCorrect, FirstViolation? | UC09 Output |
| `HintResult` | RowIdx, ColIdx, Value | UC07 Output |
| `SolveResult` | Solutions (0\|1\|2), Solution? (int[,]) | UC11 Output |
| `HighscoreEntry` | Rank, Username, Difficulty, TimeSeconds, HintsUsed, Score | UC08 Output |

## Kritische Abhängigkeiten (für Test-Order)

```
ISolverService    ← Pure Logic (Unit-Test ZUERST)
  ↑
IPuzzleService.SaveIfSolvableAsync (UC05)
IGameService.CheckSolutionAsync   (UC09)
IHintService.GetHintAsync         (UC07)
```

→ Solver-Tests sind **kritisch**: 5+ Unit-Tests (Beispiele aus README, Edge-Cases, Unsolvable, Multi-Solution).
