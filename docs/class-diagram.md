# Class Diagram — Killer Sudoku

> **Spec-Bezug:** Pflicht-Sektion gemäss `skillsbattle2026_1.1.md` §2.6
> **Stack:** .NET 10, Blazor Server, EF Core 10, MS-SQL Express
> **Querverweise:** [`functionality.md`](functionality.md) (autoritative Service-Signaturen) · [`erm.md`](erm.md) (Daten-Modell) · [`use-cases.md`](use-cases.md)

Das System ist in **drei .NET-Projekte** aufgeteilt, jedes Projekt = ein Layer:

| Projekt | Layer | Verantwortung |
|---------|-------|---------------|
| `KillerSudoku.Web` | Presentation | Blazor Server Pages + Components |
| `KillerSudoku.Core` | Application + Domain | Services + reine Sudoku-Algorithmik |
| `KillerSudoku.Data` | Persistence | EF Core DbContext + Entities |

---

## 1 Übersichts-Klassendiagramm (alle Layer)

```mermaid
classDiagram
    direction TB

    %% ============ PRESENTATION ============
    class PlayPuzzle {
        +int PuzzleId
        +OnInitializedAsync() Task
        -HandleCellInput(int row, int col, int? v) Task
        -HandleHintClick() Task
        -HandleCheckClick() Task
    }
    class EnterPuzzle {
        -PuzzleInputDto Draft
        -HandleSave() Task
    }
    class PuzzleList {
        -int? FilterDifficulty
        -int Page
        -LoadAsync() Task
    }
    class Highscore {
        -List~HighscoreEntry~ Top
        -LoadAsync() Task
    }
    class Login {
        -LoginDto Form
        -HandleSubmit() Task
    }
    class Register {
        -RegisterDto Form
        -HandleSubmit() Task
    }
    class Home {
        +OnInitialized() void
    }
    class PuzzleGrid {
        +int[,] Values
        +Action~int,int,int?~ OnCellInput
    }
    class CageEditor {
        +List~CageInputDto~ Cages
        +Action~CageInputDto~ OnCageAdded
    }
    class HintButton {
        +Action OnClick
    }
    class CheckSolutionButton {
        +Action OnClick
    }
    class PauseButton {
        +bool IsPaused
        +Action OnToggle
    }
    class PencilMarkLayer {
        +Dictionary~Cell,HashSet~int~~ Marks
    }
    class HighscoreTable {
        +IReadOnlyList~HighscoreEntry~ Rows
    }

    %% ============ APPLICATION SERVICES ============
    class IPuzzleService {
        <<interface>>
        +ValidateStructureAsync(PuzzleInputDto) Task~ValidationResult~
        +SaveIfSolvableAsync(PuzzleInputDto, int userId) Task~SavePuzzleResult~
        +ListAsync(byte? difficulty, int page, int pageSize, int? currentUserId) Task~PagedResult~PuzzleSummary~~
        +GetByIdAsync(int puzzleId) Task~PuzzleInputDto?~
    }
    class IGameService {
        <<interface>>
        +StartGameAsync(int userId, int puzzleId) Task~int~
        +SetCellValueAsync(int gameId, byte row, byte col, byte? v) Task
        +TogglePencilMarkAsync(int gameId, byte row, byte col, byte mark) Task
        +PauseAsync(int gameId) Task
        +ResumeAsync(int gameId) Task
        +CheckSolutionAsync(int gameId) Task~CheckResult~
        +CompleteGameAsync(int gameId) Task~int~
    }
    class IHintService {
        <<interface>>
        +GetHintAsync(int gameId) Task~HintResult~
    }
    class IHighscoreService {
        <<interface>>
        +GetTopAsync(int limit, byte? difficulty) Task~IReadOnlyList~HighscoreEntry~~
    }
    class IPuzzleGenerator {
        <<interface>>
        +Generate(byte difficulty, Random? rng) PuzzleInputDto
    }
    class IScoreCalculator {
        <<interface>>
        +Calculate(int timeSeconds, int hintsUsed) int
    }
    class PuzzleService
    class GameService
    class HintService
    class HighscoreService
    class PuzzleGenerator
    class ScoreCalculator
    class AspNetIdentity {
        <<framework>>
        UserManager~AppUser~
        SignInManager~AppUser~
    }

    %% ============ DOMAIN ============
    class ISolverService {
        <<interface>>
        +Solve(byte[,] givens, IReadOnlyList~CageInputDto~ cages) SolveResult
        +CountSolutions(byte[,] givens, IReadOnlyList~CageInputDto~ cages, int limit) int
    }
    class SolverService {
        -Backtrack(SolverContext ctx, int limit) bool
    }
    class SolutionValidator {
        +Validate(byte[,] grid, IReadOnlyList~CageInputDto~ cages) CheckResult
    }
    class PuzzleStructureValidator {
        +Validate(PuzzleInputDto input) ValidationResult
    }
    class PuzzleGenerator {
        +Generate(byte difficulty, Random? rng) PuzzleInputDto
    }
    class ScoreCalculator {
        +Calculate(int timeSeconds, int hintsUsed) int
    }
    note for SolverService "Inline-Hint-Strategien in HintService\n(Naked Single, Cage-Forced, Solver-Fallback);\nkeine eigene HintStrategies-Klasse."

    %% ============ DTOs ============
    class PuzzleInputDto
    class CageInputDto
    class PuzzleSummary
    class CheckResult
    class HintResult
    class SolveResult
    class HighscoreEntry
    class SavePuzzleResult
    class ValidationResult
    class PagedResult~T~
    note for SavePuzzleResult "Register/Login verwenden\nIdentityResult/SignInResult\naus ASP.NET Identity\n(keine eigenen DTOs)"

    %% ============ PERSISTENCE ============
    class SudokuDbContext {
        <<IdentityDbContext>>
        +DbSet~Puzzle~ Puzzles
        +DbSet~Cage~ Cages
        +DbSet~CageCell~ CageCells
        +DbSet~Game~ Games
        +DbSet~GameCell~ GameCells
        +DbSet~PencilMark~ PencilMarks
        +DbSet~HintLog~ HintLogs
        +OnModelCreating(ModelBuilder) void
    }
    class AppUser {
        <<IdentityUser~int~>>
    }
    class Puzzle
    class Cage
    class CageCell
    class Game
    class GameCell
    class PencilMark
    class HintLog

    %% ============ DEPENDENCIES ============
    PlayPuzzle ..> IGameService
    PlayPuzzle ..> IHintService
    PlayPuzzle ..> SudokuDbContext
    EnterPuzzle ..> IPuzzleService
    EnterPuzzle ..> IPuzzleGenerator
    PuzzleList ..> IPuzzleService
    Highscore ..> IHighscoreService
    Login ..> AspNetIdentity
    Register ..> AspNetIdentity

    IPuzzleService <|.. PuzzleService
    IGameService <|.. GameService
    IHintService <|.. HintService
    IHighscoreService <|.. HighscoreService
    ISolverService <|.. SolverService
    IPuzzleGenerator <|.. PuzzleGenerator
    IScoreCalculator <|.. ScoreCalculator

    PuzzleService ..> SudokuDbContext
    PuzzleService ..> ISolverService
    PuzzleService ..> PuzzleStructureValidator
    GameService ..> SudokuDbContext
    GameService ..> SolutionValidator
    GameService ..> IScoreCalculator
    HintService ..> SudokuDbContext
    HintService ..> ISolverService
    HighscoreService ..> SudokuDbContext
    PuzzleGenerator ..> ISolverService

    SudokuDbContext --> AppUser
    SudokuDbContext --> Puzzle
    SudokuDbContext --> Cage
    SudokuDbContext --> CageCell
    SudokuDbContext --> Game
    SudokuDbContext --> GameCell
    SudokuDbContext --> PencilMark
    SudokuDbContext --> HintLog
```

> **Auth-Hinweis:** Es gibt **keinen** eigenen `IAuthService`. UC02 (Register) und UC03 (Login) werden direkt in den Razor-Pages über `UserManager<AppUser>` / `SignInManager<AppUser>` von ASP.NET Core Identity umgesetzt. Logout ist als `POST /logout`-Endpoint in `Program.cs` registriert.

---

## 2 Domain-Kern (KillerSudoku.Core / Domain)

Reine C#-Algorithmik. **Keine** EF-Core- oder Framework-Abhängigkeit — komplett unit-testbar.

```mermaid
classDiagram
    direction LR

    class ISolverService {
        <<interface>>
        +Solve(byte[,] givens, IReadOnlyList~CageInputDto~ cages) SolveResult
        +CountSolutions(byte[,] givens, IReadOnlyList~CageInputDto~ cages, int limit) int
    }

    class SolverService {
        <<sealed>>
        +Solve(byte[,] givens, IReadOnlyList~CageInputDto~ cages) SolveResult
        +CountSolutions(byte[,] givens, IReadOnlyList~CageInputDto~ cages, int limit=2) int
        -Backtrack(SolverContext ctx, int limit) bool
    }

    class SolverContext {
        <<nested,private>>
        +byte[,] Grid
        +int[] RowMask
        +int[] ColMask
        +int[] NonetMask
        +int[] CageId
        +int[] CageUsedMask
        +int[] CageRemainingSum
    }

    class SolutionValidator {
        +Validate(byte[,] grid, IReadOnlyList~CageInputDto~ cages) CheckResult
    }

    class PuzzleStructureValidator {
        +Validate(PuzzleInputDto input) ValidationResult
    }

    class PuzzleGenerator {
        <<sealed>>
        +Generate(byte difficulty, Random? rng) PuzzleInputDto
    }

    class CageInputDto {
        <<record>>
        +byte Sum
        +IReadOnlyList~CellCoord~ Cells
    }

    class CellCoord {
        <<record>>
        +byte Row
        +byte Col
    }

    class CheckFailReason {
        <<enumeration>>
        SumMismatch
        RowDuplicate
        ColumnDuplicate
        NonetDuplicate
        CageSumMismatch
        CageDuplicate
        Incomplete
    }

    class SolveResult {
        <<record>>
        +int Solutions
        +byte[,]? Solution
    }

    class HintResult {
        <<record>>
        +byte RowIdx
        +byte ColIdx
        +byte Value
        +HintStrategy Strategy
    }

    class HintStrategy {
        <<enumeration>>
        NakedSingle
        CageForced
        SolverFallback
    }

    class CheckResult {
        <<record>>
        +bool IsCorrect
        +CheckFailReason? FailReason
    }

    ISolverService <|.. SolverService
    SolverService *-- SolverContext
    SolverService ..> CageInputDto
    SolverService ..> SolveResult
    SolutionValidator ..> CageInputDto
    SolutionValidator ..> CheckResult
    SolutionValidator ..> CheckFailReason
    PuzzleStructureValidator ..> PuzzleInputDto
    PuzzleGenerator ..> ISolverService
    HintResult ..> HintStrategy
    CageInputDto ..> CellCoord
```

**Bemerkungen:**

- `SolverService.Backtrack` ist Backtracking + MRV-Heuristik + Bit-Masken-Repräsentation für Row/Col/Nonet/Cage-Constraints (siehe `Core/Services/SolverService.cs`).
- `CountSolutions(..., limit)` bricht beim Erreichen des Limits ab — Default `limit=2` (Spec UC5: "solution must be unique") → Solutions ∈ {0, 1, 2}.
- **Sum-Check 9 × 45 = 405** ist als Pre-Check inline implementiert (`Core/Services/SolutionValidator.cs` und `Core/Services/SolverService.cs:SolverContext.Create`); keine eigene Methode `CheckSumIs405()`.
- `SolutionValidator.Validate` setzt die vier Killer-Sudoku-Regeln in einer fest definierten Reihenfolge um: Vollständigkeit → Sum=405 → Row/Col/Nonet → Cage-Sum/Cage-Duplikat (FailReason codiert via `CheckFailReason`-Enum).
- **Hint-Strategien (Naked Single / Cage-Forced / Solver-Fallback)** sind nicht als eigene Klasse `HintStrategies` modelliert, sondern als private Methoden in `HintService` (`Data/Services/HintService.cs`).

---

## 3 Application Services (KillerSudoku.Core / Application)

Use-Case-Orchestrierung. Jeder Service implementiert genau ein Interface (für Mocking in Unit-Tests).

```mermaid
classDiagram
    direction LR

    class IAuthService {
        <<interface>>
        +RegisterAsync(RegisterDto) Task~RegisterResult~
        +LoginAsync(LoginDto) Task~LoginResult~
        +LogoutAsync(int userId) Task
    }
    class AuthService {
        -SudokuDbContext _db
        -IPasswordHasher _hasher
        +RegisterAsync(RegisterDto) Task~RegisterResult~
        +LoginAsync(LoginDto) Task~LoginResult~
        +LogoutAsync(int userId) Task
    }

    class IPuzzleService {
        <<interface>>
        +ValidateStructureAsync(PuzzleInputDto) Task~ValidationResult~
        +SaveIfSolvableAsync(PuzzleInputDto, int userId) Task~SavePuzzleResult~
        +ListAsync(int? difficulty, int page, int pageSize) Task~PageResult~PuzzleListItem~~
        +GetByIdAsync(int puzzleId) Task~PuzzleDto~
    }
    class PuzzleService {
        -SudokuDbContext _db
        -ISolverService _solver
        +ValidateStructureAsync(PuzzleInputDto) Task~ValidationResult~
        +SaveIfSolvableAsync(PuzzleInputDto, int userId) Task~SavePuzzleResult~
        +ListAsync(int? difficulty, int page, int pageSize) Task~PageResult~PuzzleListItem~~
        +GetByIdAsync(int puzzleId) Task~PuzzleDto~
    }

    class IGameService {
        <<interface>>
        +StartGameAsync(int userId, int puzzleId) Task~int~
        +SetCellValueAsync(int gameId, int row, int col, int? v) Task
        +TogglePencilMarkAsync(int gameId, int row, int col, int mark) Task
        +PauseAsync(int gameId) Task
        +ResumeAsync(int gameId) Task
        +CheckSolutionAsync(int gameId) Task~CheckResult~
        +CompleteGameAsync(int gameId) Task~int~
    }
    class GameService {
        -SudokuDbContext _db
        -SolutionValidator _validator
        +StartGameAsync(int userId, int puzzleId) Task~int~
        +SetCellValueAsync(int gameId, int row, int col, int? v) Task
        +TogglePencilMarkAsync(int gameId, int row, int col, int mark) Task
        +PauseAsync(int gameId) Task
        +ResumeAsync(int gameId) Task
        +CheckSolutionAsync(int gameId) Task~CheckResult~
        +CompleteGameAsync(int gameId) Task~int~
        -CalcScore(int seconds, int hints) int
    }

    class IHintService {
        <<interface>>
        +GetHintAsync(int gameId) Task~HintResult~
    }
    class HintService {
        -SudokuDbContext _db
        -ISolverService _solver
        -HintStrategies _strategies
        +GetHintAsync(int gameId) Task~HintResult~
    }

    class IHighscoreService {
        <<interface>>
        +GetTopAsync(int limit) Task~IReadOnlyList~HighscoreEntry~~
    }
    class HighscoreService {
        -SudokuDbContext _db
        +GetTopAsync(int limit) Task~IReadOnlyList~HighscoreEntry~~
    }

    IAuthService <|.. AuthService
    IPuzzleService <|.. PuzzleService
    IGameService <|.. GameService
    IHintService <|.. HintService
    IHighscoreService <|.. HighscoreService
```

**UC-Mapping:** vollständig in [`functionality.md`](functionality.md) §Matrix.

**Wichtige Invarianten:**

| Service | Invariante |
|---------|-----------|
| `PuzzleService.SaveIfSolvableAsync` | persistiert nur wenn `_solver.CountSolutions(...) == 1` (Spec UC5 + UC4 "unique") |
| `GameService.CheckSolutionAsync` | Reihenfolge: (1) `CheckSumIs405`, (2) Row/Col/Nonet, (3) Cage — billig→teuer |
| `GameService.CompleteGameAsync` | Score = `max(0, 10000 − TimeSeconds − HintsUsed × 300)` |
| `HintService.GetHintAsync` | inkrementiert `Game.HintsUsed`, schreibt `HintLog`-Eintrag |

---

## 4 DTOs / Records (Data Transfer)

Alle DTOs sind C#-`record`s (immutable, `with`-Syntax, value-based equality → erleichtert Unit-Tests).

```mermaid
classDiagram
    direction TB

    class RegisterDto {
        <<record>>
        +string Username
        +string Email
        +string Password
        +string PasswordConfirm
    }
    class LoginDto {
        <<record>>
        +string UsernameOrEmail
        +string Password
    }
    class PuzzleInputDto {
        <<record>>
        +int Difficulty
        +IReadOnlyList~CageInputDto~ Cages
    }
    class CageInputDto {
        <<record>>
        +int Sum
        +IReadOnlyList~CellCoord~ Cells
    }
    class CellCoord {
        <<record>>
        +int Row
        +int Col
    }
    class PuzzleDto {
        <<record>>
        +int Id
        +int Difficulty
        +string CreatedByUsername
        +DateTime CreatedAt
        +IReadOnlyList~CageDto~ Cages
    }
    class CageDto {
        <<record>>
        +int Id
        +int Sum
        +IReadOnlyList~CellCoord~ Cells
    }
    class PuzzleListItem {
        <<record>>
        +int Id
        +int Difficulty
        +string CreatedBy
        +DateTime CreatedAt
        +int? MyBestScore
    }
    class HighscoreEntry {
        <<record>>
        +int Rank
        +string Username
        +int Difficulty
        +int TimeSeconds
        +int HintsUsed
        +int Score
        +DateTime CompletedAt
    }
    class RegisterResult {
        <<record>>
        +bool Success
        +int? UserId
        +string[] Errors
    }
    class LoginResult {
        <<record>>
        +bool Success
        +int? UserId
        +string? Username
        +string[] Errors
    }
    class SavePuzzleResult {
        <<record>>
        +bool Saved
        +int? PuzzleId
        +SaveFailReason? FailReason
    }
    class SaveFailReason {
        <<enumeration>>
        StructureInvalid
        Unsolvable
        MultipleSolutions
    }
    class ValidationResult {
        <<record>>
        +bool IsValid
        +string[] Errors
    }
    class PageResult~T~ {
        <<record>>
        +IReadOnlyList~T~ Items
        +int TotalCount
        +int Page
        +int PageSize
    }

    PuzzleInputDto --> CageInputDto
    CageInputDto --> CellCoord
    PuzzleDto --> CageDto
    CageDto --> CellCoord
    SavePuzzleResult --> SaveFailReason
```

---

## 5 Persistence (KillerSudoku.Data / EF Core)

EF Core Entities. Schema = `db/sudoku.sql`, ERD = [`erm.md`](erm.md). FK-Kardinalitäten siehe ERD.

```mermaid
classDiagram
    direction TB

    class SudokuDbContext {
        +DbSet~AppUser~ AppUsers
        +DbSet~Puzzle~ Puzzles
        +DbSet~Cage~ Cages
        +DbSet~CageCell~ CageCells
        +DbSet~Game~ Games
        +DbSet~GameCell~ GameCells
        +DbSet~PencilMark~ PencilMarks
        +DbSet~HintLog~ HintLogs
        +DbSet~HighscoreEntry~ HighscoreView
        #OnModelCreating(ModelBuilder b) void
    }

    class AppUser {
        +int Id
        +string Username
        +string Email
        +string PasswordHash
        +DateTime CreatedAt
        +ICollection~Puzzle~ CreatedPuzzles
        +ICollection~Game~ Games
    }
    class Puzzle {
        +int Id
        +byte Difficulty
        +int CreatedById
        +DateTime CreatedAt
        +AppUser CreatedBy
        +ICollection~Cage~ Cages
        +ICollection~Game~ Games
    }
    class Cage {
        +int Id
        +int PuzzleId
        +byte Sum
        +Puzzle Puzzle
        +ICollection~CageCell~ Cells
    }
    class CageCell {
        +int CageId
        +byte RowIdx
        +byte ColIdx
        +Cage Cage
    }
    class Game {
        +int Id
        +int UserId
        +int PuzzleId
        +DateTime StartTime
        +DateTime? EndTime
        +int TimeSeconds
        +int HintsUsed
        +int Score
        +bool IsCompleted
        +bool IsPaused
        +DateTime? PausedAt
        +int TotalPausedSeconds
        +AppUser User
        +Puzzle Puzzle
        +ICollection~GameCell~ Cells
        +ICollection~PencilMark~ PencilMarks
        +ICollection~HintLog~ HintLog
    }
    class GameCell {
        +int GameId
        +byte RowIdx
        +byte ColIdx
        +byte? CellValue
        +Game Game
    }
    class PencilMark {
        +int GameId
        +byte RowIdx
        +byte ColIdx
        +byte MarkValue
        +Game Game
    }
    class HintLog {
        +int Id
        +int GameId
        +byte RowIdx
        +byte ColIdx
        +DateTime HintAt
        +Game Game
    }

    AppUser "1" --> "0..*" Puzzle : creates
    AppUser "1" --> "0..*" Game : plays
    Puzzle "1" --> "1..*" Cage : has
    Puzzle "1" --> "0..*" Game : "played as"
    Cage "1" --> "1..*" CageCell : contains
    Game "1" --> "1..*" GameCell : tracks
    Game "1" --> "0..*" PencilMark : marks
    Game "1" --> "0..*" HintLog : logs
    SudokuDbContext ..> AppUser
    SudokuDbContext ..> Puzzle
    SudokuDbContext ..> Cage
    SudokuDbContext ..> CageCell
    SudokuDbContext ..> Game
    SudokuDbContext ..> GameCell
    SudokuDbContext ..> PencilMark
    SudokuDbContext ..> HintLog
```

**Bemerkungen:**

- `HighscoreView` ist als **Keyless-Entity** registriert (Mapping zu `vw_Highscore`).
- `CageCell` / `GameCell` / `PencilMark` haben **Composite PK** (siehe `sudoku.sql`).
- Trigger `trg_CageCell_UniquePerPuzzle` lebt in SQL, **nicht** in C#.

---

## 6 Presentation (KillerSudoku.Web / Blazor Server)

Auszug der wichtigsten Pages und Components. Vollständige Component-Liste siehe [`arc42/05-building-blocks.md`](arc42/05-building-blocks.md) §5.2.1.

```mermaid
classDiagram
    direction TB

    class ComponentBase {
        <<Blazor>>
        #OnInitialized() void
        #OnInitializedAsync() Task
        #StateHasChanged() void
    }

    class MainLayout {
        -bool IsAuthenticated
    }
    class NavMenu {
        +string? CurrentUserName
    }

    class Home {
        -bool RulesExpanded
    }
    class Register {
        -RegisterDto Form
        -string[] Errors
        -HandleSubmit() Task
    }
    class Login {
        -LoginDto Form
        -string[] Errors
        -HandleSubmit() Task
    }
    class PuzzleList {
        -int? FilterDifficulty
        -int Page = 1
        -PageResult~PuzzleListItem~? Data
        -LoadAsync() Task
    }
    class EnterPuzzle {
        -PuzzleInputDto Draft
        -SavePuzzleResult? LastSave
        -HandleAddCage(CageInputDto) void
        -HandleSave() Task
    }
    class PlayPuzzle {
        +int PuzzleId
        -int GameId
        -int[,] Values
        -int HintsUsed
        -TimeSpan Elapsed
        -bool IsPaused
        -bool PencilMode
        -HandleCellInput(int row, int col, int? v) Task
        -HandleHintClick() Task
        -HandleCheckClick() Task
        -HandlePauseToggle() Task
    }
    class Highscore {
        -List~HighscoreEntry~ Top
        -LoadAsync() Task
    }

    class PuzzleGrid {
        +int[,] Values
        +IReadOnlyList~CageDef~ Cages
        +bool ReadOnly
        +EventCallback~CellInputArgs~ OnCellInput
    }
    class CageEditor {
        +EventCallback~CageInputDto~ OnCageAdded
        -List~CellCoord~ Selected
        -int? Sum
    }
    class Toolbar {
        +bool PencilMode
        +EventCallback OnTogglePencil
    }
    class HintButton {
        +int HintsUsed
        +EventCallback OnClick
    }
    class CheckSolutionButton {
        +EventCallback OnClick
    }
    class PauseButton {
        +bool IsPaused
        +EventCallback OnToggle
    }
    class PencilMarkLayer {
        +Dictionary~CellCoord, HashSet~int~~ Marks
    }
    class RulesPanel
    class MiniSudokuExample
    class FilterBar {
        +int? Difficulty
        +EventCallback~int?~ OnDifficultyChange
    }
    class PuzzleCard {
        +PuzzleListItem Item
        +EventCallback OnPlay
    }
    class HighscoreTable {
        +IReadOnlyList~HighscoreEntry~ Rows
    }
    class RegisterForm {
        +EventCallback~RegisterDto~ OnSubmit
    }
    class LoginForm {
        +EventCallback~LoginDto~ OnSubmit
    }

    ComponentBase <|-- MainLayout
    ComponentBase <|-- NavMenu
    ComponentBase <|-- Home
    ComponentBase <|-- Register
    ComponentBase <|-- Login
    ComponentBase <|-- PuzzleList
    ComponentBase <|-- EnterPuzzle
    ComponentBase <|-- PlayPuzzle
    ComponentBase <|-- Highscore
    ComponentBase <|-- PuzzleGrid
    ComponentBase <|-- CageEditor
    ComponentBase <|-- Toolbar
    ComponentBase <|-- HintButton
    ComponentBase <|-- CheckSolutionButton
    ComponentBase <|-- PauseButton
    ComponentBase <|-- PencilMarkLayer
    ComponentBase <|-- RulesPanel
    ComponentBase <|-- MiniSudokuExample
    ComponentBase <|-- FilterBar
    ComponentBase <|-- PuzzleCard
    ComponentBase <|-- HighscoreTable
    ComponentBase <|-- RegisterForm
    ComponentBase <|-- LoginForm

    Home --> RulesPanel
    Home --> MiniSudokuExample
    Register --> RegisterForm
    Login --> LoginForm
    PuzzleList --> FilterBar
    PuzzleList --> PuzzleCard
    EnterPuzzle --> PuzzleGrid
    EnterPuzzle --> CageEditor
    PlayPuzzle --> PuzzleGrid
    PlayPuzzle --> Toolbar
    PlayPuzzle --> HintButton
    PlayPuzzle --> CheckSolutionButton
    PlayPuzzle --> PauseButton
    PlayPuzzle --> PencilMarkLayer
    Highscore --> HighscoreTable
```

---

## 7 Dependency Injection — Registrierungs-Übersicht

```mermaid
flowchart LR
    subgraph DIRoot["Program.cs / DI Container"]
        direction TB
        AS["IAuthService → AuthService"]:::svc
        PS["IPuzzleService → PuzzleService"]:::svc
        GS["IGameService → GameService"]:::svc
        HS["IHintService → HintService"]:::svc
        HighS["IHighscoreService → HighscoreService"]:::svc
        Solver["ISolverService → SolverService (Singleton)"]:::dom
        Validator["SolutionValidator (Singleton)"]:::dom
        Hints["HintStrategies (Singleton)"]:::dom
        Ctx["SudokuDbContext (Scoped)"]:::data
        Hasher["IPasswordHasher → ASP.NET Identity"]:::sec
    end

    PS --> Solver
    GS --> Validator
    HS --> Solver
    HS --> Hints
    AS --> Hasher
    AS --> Ctx
    PS --> Ctx
    GS --> Ctx
    HS --> Ctx
    HighS --> Ctx

    classDef svc fill:#e6f3ff,stroke:#1976d2
    classDef dom fill:#fff4e6,stroke:#f57c00
    classDef data fill:#e8f5e9,stroke:#388e3c
    classDef sec fill:#fce4ec,stroke:#c2185b
```

**Lifetime-Konventionen:**

| Kategorie | Lifetime | Begründung |
|-----------|----------|------------|
| Pure Domain (Solver, Validator, HintStrategies) | **Singleton** | Stateless, thread-safe |
| Application Services | **Scoped** | Hängen am `DbContext` (Scoped) |
| `SudokuDbContext` | **Scoped** | EF-Core-Standard, ein Context pro Request |
| Blazor Components | **Transient** (per Render) | Framework-gemanaged |

---

## 8 Test-Klassen-Mapping

Pro produktiver Klasse die zugehörige Test-Klasse:

| Production | Test-Klasse | Framework | Test-Typ |
|-----------|-------------|-----------|----------|
| `SolverService` | `SolverServiceTests` | xUnit | Unit |
| `SolutionValidator` | `SolutionValidatorTests` | xUnit | Unit |
| `HintStrategies` | `HintStrategiesTests` | xUnit | Unit |
| `AuthService` | `AuthServiceTests` | xUnit + EF InMemory | Unit / Integration |
| `PuzzleService` | `PuzzleServiceTests` | xUnit + EF InMemory | Unit / Integration |
| `GameService` | `GameServiceTests` | xUnit + EF InMemory | Unit / Integration |
| `HintService` | `HintServiceTests` | xUnit + Mock Solver | Unit |
| `HighscoreService` | `HighscoreServiceTests` | xUnit + Testcontainer-SQL | Integration |
| `PuzzleGrid.razor` | `PuzzleGridTests` | bUnit | Component |
| `HintButton.razor` | `HintButtonTests` | bUnit | Component |
| `PlayPuzzle.razor` | `PlayPuzzleTests` | bUnit + Mock Services | Component |
| `SudokuDbContext` (Schema) | `SchemaTests` | xUnit + Testcontainer-SQL | Integration |

Vollständige Test-Cases siehe [`test-protocol.md`](test-protocol.md).
