# Entity Relationship Model — Killer Sudoku

**DB-Engine:** Microsoft SQL Server Express
**Database-Name:** `sudoku`
**ORM:** Entity Framework Core 10 (`SudokuDbContext`) — Schema-Verwaltung via [`db/sudoku.sql`](../db/sudoku.sql); EF-Modellierung spiegelt das SQL-Schema (kein `dotnet ef migrations` aktiv).
**Auth-Schema:** ASP.NET Core Identity. `AppUser` erbt von `IdentityUser<int>`. Zusätzlich zu den Kern-Tabellen unten sind die Identity-Hilfstabellen `AspNetRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetUserRoles`, `AspNetRoleClaims` Teil des Schemas (siehe `db/sudoku.sql`).

## ERD (Mermaid)

Das Diagramm zeigt die fachlichen Killer-Sudoku-Tabellen plus `AppUser`. Die Identity-Hilfstabellen sind in §"Identity-Hilfstabellen" beschrieben und werden im ERD bewusst weggelassen, um die Domain-Sicht zu erhalten.

```mermaid
erDiagram
    AppUser   ||--o{ Puzzle    : creates
    AppUser   ||--o{ Game      : plays
    Puzzle    ||--|{ Cage      : contains
    Puzzle    ||--o{ Game      : "is played in"
    Cage      ||--|{ CageCell  : has
    Game      ||--|{ GameCell  : has
    Game      ||--o{ PencilMark: has
    Game      ||--o{ HintLog   : logs

    AppUser {
        int       Id                    PK
        nvarchar  UserName              UK_nullable
        nvarchar  NormalizedUserName    UK_nullable
        nvarchar  Email                 UK_nullable
        nvarchar  NormalizedEmail
        bit       EmailConfirmed
        nvarchar  PasswordHash          nullable
        nvarchar  SecurityStamp         nullable
        nvarchar  ConcurrencyStamp      nullable
        nvarchar  PhoneNumber           nullable
        bit       PhoneNumberConfirmed
        bit       TwoFactorEnabled
        datetimeoffset LockoutEnd       nullable
        bit       LockoutEnabled
        int       AccessFailedCount
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
        datetime2 EndTime            nullable
        int       TimeSeconds        nullable
        int       HintsUsed
        int       Score              nullable
        bit       IsCompleted
        bit       IsPaused
        datetime2 PausedAt           nullable
        int       TotalPausedSeconds
    }
    GameCell {
        int     GameId    PK,FK
        tinyint RowIdx    PK
        tinyint ColIdx    PK
        tinyint CellValue nullable
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

> **Anmerkung Kardinalitäten:** `Puzzle ||--|{ Cage` (1..*): ein valide gespeichertes Puzzle hat mindestens einen Cage (insgesamt 81 Zellen müssen abgedeckt sein). DB-seitig garantiert dies aktuell nicht ein expliziter Constraint, sondern die Application-Validation in `PuzzleStructureValidator` (siehe [`validation.md`](validation.md#v06--cage-struktur-uc04) V06).

## Design-Entscheidungen

| Punkt | Entscheidung | Begründung |
|-------|-------------|------------|
| User-Tabelle | `AppUser` statt `User` | `User` ist reserviertes T-SQL-Keyword |
| Spaltennamen `Row`/`Col` | `RowIdx` / `ColIdx` | `ROW`/`COL` sind T-SQL-Reserved |
| Cage-Modell | Relational (Cage + CageCell) | FK-Integrity, easy joins, prüfer-testbar via SQL |
| Solution-Speicherung | **NICHT** in DB | UC04 README: "No solution is recorded" |
| Highscore | als **VIEW** `vw_Highscore` | Single source of truth via JOIN, kein Sync-Problem |
| "Eine Zelle pro Cage in einem Puzzle" | Application-Layer + Trigger ODER UNIQUE-Index | siehe `sudoku.sql` Constraint-Diskussion |
| Game-Pause | `IsPaused` + `PausedAt` + `TotalPausedSeconds` | erlaubt sauberen Timer-Resume, `TimeSeconds = TotalPausedSeconds + (EndTime - StartTime)` |
| GameCell.CellValue | NULL erlaubt | leere Zelle vs eingetragener Wert |
| PencilMark Modell | eigene Tabelle mit Composite PK | (GameId, Row, Col, Value) — bis zu 9 Marks pro Zelle |

## Constraints (zusammengefasst)

- `AppUser.Username` UNIQUE, NOT NULL
- `AppUser.Email` UNIQUE, NOT NULL
- `Puzzle.Difficulty` CHECK ∈ {1, 2, 3}
- `Cage.Sum` CHECK BETWEEN 1 AND 45
- `CageCell.RowIdx` / `ColIdx` CHECK BETWEEN 0 AND 8
- `GameCell.CellValue` CHECK BETWEEN 1 AND 9 (oder NULL)
- `PencilMark.MarkValue` CHECK BETWEEN 1 AND 9
- `Game.TimeSeconds` CHECK ≥ 0
- `Game.HintsUsed` CHECK ≥ 0
- `Game.Score` CHECK ≥ 0
- "Eine Zelle pro Cage pro Puzzle" → Trigger + Application-Validierung (siehe SQL)

## Ableitungen für Tests

- **UC04 → AC04.4:** Test "Puzzle-Tabelle hat KEINE Solution-Spalte" = SQL-Query auf `INFORMATION_SCHEMA.COLUMNS`
- **UC04 → AC04.1:** Test "INSERT mit Difficulty=4 wird abgelehnt" = SQL-Constraint-Test
- **UC04 → AC04.3:** Test "INSERT Cage mit Sum=46 wird abgelehnt" = SQL-Constraint-Test
- **UC05 → AC05.1:** Integration-Test: Save versuche mit unlösbarem Puzzle → kein Row in `Puzzle`
- **UC13 → AC13.3:** UNIQUE-Index `UX_Game_ActiveOnly (UserId, PuzzleId)` WHERE `IsCompleted = 0` (Filtered Index) — verhindert mehrere offene Games pro User-Puzzle-Kombi (egal ob pausiert oder laufend).

## Sample-Queries (für Solver / Hint / Validation)

```sql
-- Alle Cages eines Puzzles mit ihren Zellen
SELECT c.Id, c.[Sum], cc.RowIdx, cc.ColIdx
FROM Cage c
JOIN CageCell cc ON cc.CageId = c.Id
WHERE c.PuzzleId = @PuzzleId
ORDER BY c.Id, cc.RowIdx, cc.ColIdx;

-- Validation UC09 — Sum-Check
SELECT SUM(CellValue) AS Total FROM GameCell WHERE GameId = @GameId;
-- Expect: 405

-- Validation UC09 — Cage-Sum-Check
SELECT c.Id, c.[Sum] AS Expected, SUM(gc.CellValue) AS Actual
FROM Cage c
JOIN CageCell cc ON cc.CageId = c.Id
JOIN GameCell gc ON gc.RowIdx = cc.RowIdx AND gc.ColIdx = cc.ColIdx
WHERE gc.GameId = @GameId
GROUP BY c.Id, c.[Sum]
HAVING c.[Sum] <> SUM(gc.CellValue);
-- Expect 0 rows for correct solution

-- Validation UC09 — Cage-Duplikat-Check
SELECT c.Id, gc.CellValue, COUNT(*) AS Cnt
FROM Cage c
JOIN CageCell cc ON cc.CageId = c.Id
JOIN GameCell gc ON gc.RowIdx = cc.RowIdx AND gc.ColIdx = cc.ColIdx
WHERE gc.GameId = @GameId
GROUP BY c.Id, gc.CellValue
HAVING COUNT(*) > 1;
-- Expect 0 rows for correct solution
```
