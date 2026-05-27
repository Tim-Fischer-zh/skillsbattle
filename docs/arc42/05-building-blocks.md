# 5 Bausteinsicht

> arc42 v8.2 · Kapitel 5 · Killer Sudoku
> Bezug: [Kapitel 4](04-solution-strategy.md) · [`../functionality.md`](../functionality.md) (autoritativ für Service-Signaturen) · [`../erm.md`](../erm.md) · [`../../db/sudoku.sql`](../../db/sudoku.sql)

Dieses Kapitel zerlegt das System statisch in seine Bausteine. Die Service-Signaturen werden **nicht** dupliziert — sie stehen in [`../functionality.md`](../functionality.md) §Service-Interfaces. Hier wird die Architektur-Sicht beschrieben: Zweck, Schnittstellen, enthaltene Klassen, Abhängigkeiten.

---

## 5.1 Whitebox Gesamtsystem

Die Gesamtanwendung besteht aus drei Projekten/Assemblies (vorgesehene Aufteilung in der .NET-Solution):

```mermaid
flowchart TB
    subgraph Web["KillerSudoku.Web (Blazor Server)"]
        direction TB
        Pages["Pages<br/>Home · Login · Register<br/>PuzzleList · EnterPuzzle<br/>PlayPuzzle · Highscore"]
        Layout["Shared Layout<br/>MainLayout · NavMenu"]
        UIComp["Components<br/>App.razor · Routes.razor<br/>RedirectToLogin.razor<br/>Layout/MainLayout.razor<br/>Layout/ReconnectModal.razor<br/>Shared/MiniSudokuExample.razor"]
    end

    subgraph Core["KillerSudoku.Core (.NET Class Library)"]
        direction TB
        Services["Application Services<br/>IPuzzleService · PuzzleService<br/>IGameService · GameService<br/>IHintService · HintService<br/>IHighscoreService · HighscoreService<br/>IScoreCalculator · ScoreCalculator<br/>(Auth via ASP.NET Identity SignInManager/UserManager)"]
        Domain["Domain Kern (isoliert)<br/>ISolverService · SolverService<br/>SolutionValidator<br/>PuzzleStructureValidator<br/>PuzzleGenerator (IPuzzleGenerator)"]
        Dtos["DTOs / Records<br/>RegisterDto · LoginDto<br/>PuzzleInputDto · CageInputDto<br/>CageDef · SolveResult<br/>CheckResult · HintResult<br/>PuzzleListItem · HighscoreEntry<br/>RegisterResult · LoginResult<br/>SavePuzzleResult · PageResult&lt;T&gt;"]
    end

    subgraph Data["KillerSudoku.Data (EF Core 10)"]
        direction TB
        Ctx["SudokuDbContext"]
        Entities["Entities<br/>AppUser · Puzzle<br/>Cage · CageCell<br/>Game · GameCell<br/>PencilMark · HintLog"]
        Migr["EF Migrations<br/>(Source of Truth)<br/>sudoku.sql generiert via<br/>dotnet ef migrations script"]
    end

    DB[("MS-SQL Express<br/>Database: sudoku<br/>+ vw_Highscore (View)")]

    Pages --> UIComp
    Pages --> Layout
    UIComp -->|"DI"| Services
    Services -->|"verwendet"| Domain
    Services -->|"verwendet"| Dtos
    Services -->|"verwendet"| Ctx
    Domain -->|"verwendet"| Dtos
    Ctx --> Entities
    Migr --> DB
    Ctx --> DB
```

**Projekt-Verantwortlichkeiten kurz:**

- `KillerSudoku.Web` — UI-Schicht; rein präsentational; keine Geschäftslogik; ruft Services per DI auf.
- `KillerSudoku.Core` — Application Services + isolierter Domain-Kern (Solver/Validator) + DTOs; **keine** EF-Core-Abhängigkeit im Domain-Kern.
- `KillerSudoku.Data` — Persistenz; EF-Core-DbContext, Entity-Mappings, Migrations. Wird von Application-Services verwendet, aber nicht von der UI direkt.

---

## 5.2 Baustein-Tabellen (Whitebox je Layer)

Für jeden Baustein: Zweck · Schnittstellen (eingehend/ausgehend) · enthaltene Klassen/Komponenten. Service-Signaturen siehe [Funktionalitäts-Matrix](../functionality.md).

### 5.2.1 KillerSudoku.Web — Presentation Layer

| Aspekt | Details |
|--------|---------|
| **Zweck** | Blazor-Server-Anwendung; rendert UI, leitet User-Aktionen an Application-Services weiter, zeigt Ergebnisse. |
| **Eingehende Schnittstelle** | HTTP-Requests vom Browser (Routes aus [Funktionalitäts-Matrix](../functionality.md) §Screen-Inventar S1–S8); SignalR-Verbindung für interaktive Updates. |
| **Ausgehende Schnittstelle** | Application-Services via DI: `IPuzzleService`, `IGameService`, `IHintService`, `IHighscoreService`. Auth direkt über ASP.NET-Identity-`SignInManager<AppUser>` und `UserManager<AppUser>` (kein eigener Wrapper-Service). |
| **Pages (`@page`)** | `Home.razor` (S1, `/`) · `Auth/Register.razor` (S2, `/register`) · `Auth/Login.razor` (S3, `/login`) · `Puzzles.razor` (S4, `/puzzles`) · `EnterPuzzle.razor` (S5, `/puzzles/new`) · `PlayPuzzle.razor` (S6, `/puzzles/{id}/play`) · `Highscore.razor` (S7, `/highscore`). |
| **Shared Components** | `App.razor` · `Routes.razor` · `RedirectToLogin.razor` · `MainLayout.razor` · `ReconnectModal.razor` · `MiniSudokuExample.razor` · `Error.razor` · `NotFound.razor`. |
| **Querschnitt** | `[Authorize]` auf allen Pages außer S1/S2/S3 ([V16](../validation.md#v16--authorization-alle-geschützten-seitenendpoints)); `EditForm` mit Antiforgery ([V15](../validation.md#v15--csrf-alle-post-endpoints)); Razor-`@`-Encoding ([V14](../validation.md#v14--xss--output-encoding-alle-screens)). |

**Inline-UI:** Die im class-diagram.md skizzierten Sub-Komponenten (`PuzzleGrid`, `Toolbar`, `CageEditor`, `HintButton`, `PauseButton`, `CheckSolutionButton`, `PencilMarkLayer`, `FilterBar`, `PuzzleCard`, `HighscoreTable`, `RegisterForm`, `LoginForm`, `RulesPanel`, `NavMenu`) sind aktuell **nicht als eigene .razor-Dateien implementiert** — ihre Markup-/Event-Handler-Logik lebt inline in den Pages (`PlayPuzzle.razor`, `EnterPuzzle.razor`, `Puzzles.razor`, `Highscore.razor`, `Home.razor`, `Auth/Login.razor`, `Auth/Register.razor`). Dies hält den Component-Tree flach. Eine spätere Extraktion ist möglich, ohne API-Brüche.
| **Test-Strategie** | bUnit-Component-Tests (Rendering, Event-Handler), E2E ohne Framework gemäß README §3.2. |

### 5.2.2 KillerSudoku.Core — Application Services

| Aspekt | Details |
|--------|---------|
| **Zweck** | Use-Case-Orchestrierung; konvertiert UI-Eingaben in DB-Operationen + Domain-Aufrufe; setzt Transaktions- und Autorisierungs-Grenzen. |
| **Eingehende Schnittstelle** | Service-Interfaces `IPuzzleService`, `IGameService`, `IHintService`, `IHighscoreService` — komplette Signaturen [siehe Funktionalitäts-Matrix](../functionality.md). Auth-Flows (UC02/UC03) verwenden direkt ASP.NET-Identity (`SignInManager<AppUser>`, `UserManager<AppUser>`) ohne eigene Wrapper-Service-Schicht. |
| **Ausgehende Schnittstelle** | `ISolverService` (Domain) + `SudokuDbContext` (Data). |
| **Enthaltene Services** | `PuzzleService` (UC04/UC05/UC12) · `GameService` (UC06/UC09/UC10/UC13/UC14) · `HintService` (UC07) · `HighscoreService` (UC08) · **`ScoreCalculator`** (Singleton, pure Funktion — wird von `GameService.CompleteGameAsync` aufgerufen). UC02/UC03 (Register/Login) werden direkt über ASP.NET-Identity-Services (`SignInManager`, `UserManager`) in den Razor-Pages umgesetzt. |
| **UC-Mapping** | Vollständig in [Funktionalitäts-Matrix](../functionality.md) §Matrix-Tabelle. |
| **Test-Strategie** | Unit-Tests pro Service mit gemocktem `ISolverService` + `SudokuDbContext` (EF InMemory); Integration-Tests gegen echte MS-SQL-Test-DB. |

**HighscoreService — View-Status:** Der Service liest aktuell mit LINQ-Joins über `Games ⋈ AppUser ⋈ Puzzle`. Die View `vw_Highscore` existiert zwar im SQL-Schema (`db/sudoku.sql`), wird aber vom `SudokuDbContext` **nicht** als Keyless-Entity gemappt. Switch ist offen — Schema-Reserve.

### 5.2.3 KillerSudoku.Core — Domain-Kern (Solver/Validator)

| Aspekt | Details |
|--------|---------|
| **Zweck** | Reine Algorithmik für die vier Killer-Sudoku-Regeln aus README §1: (1) "Each row, column, and nonet contains each number exactly once", (2) "The sum of all numbers in a cage must match the small number printed in its corner", (3) "No number appears more than once in a cage", (4) "The solution must be unique". |
| **Eingehende Schnittstelle** | `ISolverService` — Methoden `Solve(givenValues, cages)` und `CountSolutions(givenValues, cages)` ([Funktionalitäts-Matrix](../functionality.md) §Service-Interfaces). |
| **Ausgehende Schnittstelle** | **Keine** — pure C# ohne Framework- oder DB-Abhängigkeit. |
| **Enthaltene Klassen** | `SolverService` (Backtracker + Constraint-Propagation) · `SolutionValidator` (Row/Col/Nonet/Cage-Check für UC09 §6.3) · `HintStrategies` (Naked Single, Cage-Forced; siehe UC07 in [`../use-cases.md`](../use-cases.md)) · interne Records `CageDef`, `Board`. |
| **Performance-Anforderung** | AC11.2: terminiert auch bei schweren Puzzles in < 2 s; bricht bei Zähl-Modus nach der 2. gefundenen Lösung ab. |
| **Test-Strategie** | Reine Unit-Tests mit den 2 README-Beispielen (§1.2) + Edge-Cases: unsolvable, multi-solution, vollständig vor-gelöst, minimal-clue. Höchste Test-Coverage-Priorität (siehe [Funktionalitäts-Matrix](../functionality.md) §Kritische Abhängigkeiten). |

### 5.2.4 KillerSudoku.Data — Persistence Layer

| Aspekt | Details |
|--------|---------|
| **Zweck** | EF-Core-Persistenz; Mapping C#-Entities ↔ T-SQL-Tabellen aus [`../../db/sudoku.sql`](../../db/sudoku.sql); Query-Optimierung; View-Zugriff. |
| **Eingehende Schnittstelle** | `SudokuDbContext` (DbSet&lt;T&gt; für jede Entity). |
| **Ausgehende Schnittstelle** | T-SQL gegen MS-SQL Express via ADO.NET-Provider. |
| **Enthaltene Entities** | `AppUser` · `Puzzle` · `Cage` · `CageCell` · `Game` · `GameCell` · `PencilMark` · `HintLog`. Schema-Quelle: [`../../db/sudoku.sql`](../../db/sudoku.sql), ERD: [`../erm.md`](../erm.md). |
| **View-Read-Model** | `vw_Highscore` (siehe `sudoku.sql` Zeilen 215–231) — wird per `Set<HighscoreEntry>().FromSqlRaw(...)` oder Keyless-Entity gelesen. |
| **DB-Constraints (Defense-Layer)** | Siehe §5.4 unten. |
| **Test-Strategie** | Integration-Tests mit Testcontainers (MS-SQL-Image) oder lokaler Test-DB; Constraint-Tests (INSERT mit Difficulty=4 muss fehlschlagen) gemäß [`../erm.md`](../erm.md) §Ableitungen für Tests. |

---

## 5.3 ER-Modell (Verweis + Einbettung)

Das ER-Modell ist autoritativ in [`../erm.md`](../erm.md) dokumentiert, inklusive Design-Entscheidungen, Constraints und Sample-Queries. Für die PDF-Lesbarkeit ist das Mermaid-ERD hier eingebettet:

```mermaid
erDiagram
    AppUser   ||--o{ Puzzle    : creates
    AppUser   ||--o{ Game      : plays
    Puzzle    ||--o{ Cage      : contains
    Puzzle    ||--o{ Game      : "is played in"
    Cage      ||--|{ CageCell  : has
    Game      ||--|{ GameCell  : has
    Game      ||--o{ PencilMark: has
    Game      ||--o{ HintLog   : logs

    AppUser {
        int      Id           PK
        nvarchar Username     UK
        nvarchar Email        UK
        nvarchar PasswordHash
        datetime2 CreatedAt
    }
    Puzzle {
        int       Id          PK
        tinyint   Difficulty
        int       CreatedById FK
        datetime2 CreatedAt
    }
    Cage {
        int     Id        PK
        int     PuzzleId  FK
        tinyint Sum
    }
    CageCell {
        int     CageId    PK,FK
        tinyint RowIdx    PK
        tinyint ColIdx    PK
    }
    Game {
        int       Id                 PK
        int       UserId             FK
        int       PuzzleId           FK
        datetime2 StartTime
        datetime2 EndTime
        int       TimeSeconds
        int       HintsUsed
        int       Score
        bit       IsCompleted
        bit       IsPaused
        datetime2 PausedAt
        int       TotalPausedSeconds
    }
    GameCell {
        int     GameId   PK,FK
        tinyint RowIdx   PK
        tinyint ColIdx   PK
        tinyint CellValue
    }
    PencilMark {
        int     GameId    PK,FK
        tinyint RowIdx    PK
        tinyint ColIdx    PK
        tinyint MarkValue PK
    }
    HintLog {
        int       Id       PK
        int       GameId   FK
        tinyint   RowIdx
        tinyint   ColIdx
        datetime2 HintAt
    }
```

**Schlüssel-Begründungen (Auszug aus [`../erm.md`](../erm.md)):**

- `AppUser` statt `User` — `USER` ist reserviertes T-SQL-Keyword.
- `RowIdx`/`ColIdx` statt `Row`/`Col` — beides reserviert.
- **Keine Solution-Spalte** in `Puzzle` — UC04 wörtlich: *"No solution is recorded. Solutions must be calculated with an algorithm"*.
- `Highscore` als **VIEW** statt denormalisierte Tabelle — single source of truth, kein Sync-Problem.
- Pause-Mechanik via `IsPaused` + `PausedAt` + `TotalPausedSeconds` → erlaubt sauberen Resume mit `TimeSeconds = DATEDIFF(SECOND, StartTime, EndTime) − TotalPausedSeconds`.

---

## 5.4 SQL-Schema-Übersicht

Vollständiges Schema in [`../../db/sudoku.sql`](../../db/sudoku.sql). Folgende Objekte werden angelegt:

**Tabellen (in Reverse-FK-Reihenfolge erzeugt):**

- `AppUser` — Benutzer mit gehashtem Passwort
  - UNIQUE: `Username`, `Email`
  - CHECK: `Username` non-empty, `Email` LIKE `%_@_%.__%`
- `Puzzle` — Killer-Sudoku-Definition, **keine** Solution-Spalte
  - FK: `CreatedById → AppUser(Id)`
  - CHECK: `Difficulty BETWEEN 1 AND 3`
- `Cage` — Cage-Gruppe mit Soll-Summe
  - FK: `PuzzleId → Puzzle(Id)` ON DELETE CASCADE
  - CHECK: `Sum BETWEEN 1 AND 45`
  - Index: `IX_Cage_PuzzleId`
- `CageCell` — Zelle in einem Cage (Composite PK)
  - PK: `(CageId, RowIdx, ColIdx)`
  - FK: `CageId → Cage(Id)` ON DELETE CASCADE
  - CHECK: `RowIdx`, `ColIdx` BETWEEN 0 AND 8
- `Game` — Spiel-Session
  - FK: `UserId → AppUser(Id)`, `PuzzleId → Puzzle(Id)`
  - CHECK: `TimeSeconds ≥ 0`, `HintsUsed ≥ 0`, `Score ≥ 0`, `TotalPausedSeconds ≥ 0`
  - Indices: `IX_Game_UserId`, `IX_Game_PuzzleId`
- `GameCell` — Aktueller Spielzustand pro Zelle
  - PK: `(GameId, RowIdx, ColIdx)`
  - FK: `GameId → Game(Id)` ON DELETE CASCADE
  - CHECK: `CellValue IS NULL OR BETWEEN 1 AND 9`
- `PencilMark` — Kandidaten-Annotationen (UC14)
  - PK: `(GameId, RowIdx, ColIdx, MarkValue)`
  - FK: `GameId → Game(Id)` ON DELETE CASCADE
  - CHECK: `MarkValue BETWEEN 1 AND 9`
- `HintLog` — Audit-Log für Hint-Nutzung (UC07)
  - FK: `GameId → Game(Id)` ON DELETE CASCADE
  - Index: `IX_HintLog_GameId`

**Index/Constraint-Spezialitäten:**

- **Trigger `trg_CageCell_UniquePerPuzzle`** — verhindert, dass eine Zelle in mehreren Cages desselben Puzzles liegt. Wirft `THROW 50001` bei Verletzung (siehe `sudoku.sql` Zeilen 100–121).
- **Filtered Unique Index `UX_Game_ActiveOnly`** — `(UserId, PuzzleId)` WHERE `IsCompleted = 0`; setzt AC13.3 durch ("max 1 aktives Game pro User-Puzzle-Kombi").

**View:**

- `vw_Highscore` — JOIN über `Game ⋈ AppUser ⋈ Puzzle` für `IsCompleted = 1 AND Score IS NOT NULL`. Liefert `GameId · UserId · Username · PuzzleId · Difficulty · TimeSeconds · HintsUsed · Score · CompletedAt`. Wird von `IHighscoreService.GetTopAsync(limit)` für UC08 verwendet.

---

> **Nächstes Kapitel:** [Kapitel 6 — Laufzeitsicht](06-runtime-view.md) — Sequenz-Diagramme der vier wichtigsten Szenarien (Puzzle-Save, Hint, Check-Solution, Pause/Resume).
