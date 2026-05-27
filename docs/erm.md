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

## Identity-Hilfstabellen (ASP.NET Core Identity)

Folgende Tabellen werden zusätzlich zu den Killer-Sudoku-Domain-Tabellen vom Identity-Subsystem benötigt und sind in `db/sudoku.sql` enthalten. Sie tauchen im ERD oben nicht auf, weil sie infrastrukturell (Cookie-Auth, Claims, Token-Storage) und nicht domain-spezifisch sind:

| Tabelle | Zweck | Wichtige Spalten |
|---------|-------|------------------|
| `AspNetRoles` | Rollen-Definitionen | `Id` (int PK), `Name`, `NormalizedName` (UQ) |
| `AspNetUserClaims` | Pro-User-Claims | `Id`, `UserId` → `AppUser` |
| `AspNetUserLogins` | Externe Login-Provider | `LoginProvider`, `ProviderKey`, `UserId` |
| `AspNetUserTokens` | Token-Storage (z. B. 2FA) | `UserId`, `LoginProvider`, `Name` |
| `AspNetUserRoles` | Junction User ↔ Role | `UserId`, `RoleId` |
| `AspNetRoleClaims` | Pro-Rolle-Claims | `Id`, `RoleId` |

Diese Tabellen werden vom Code nicht direkt referenziert; sie werden ausschließlich vom Identity-Framework gepflegt. In der aktuellen App ist nur das User-Management aktiv — die Rollen-, Claims-, Login- und Token-Mechanismen sind vorhanden, werden aber nicht genutzt.

Zusätzlich existiert `__EFMigrationsHistory` als EF-Standard-Tabelle, aktuell ungenutzt (siehe ORM-Hinweis oben).

## Design-Entscheidungen

| Punkt | Entscheidung | Begründung |
|-------|-------------|------------|
| User-Tabelle | `AppUser` (erbt von `IdentityUser<int>`) | `User` ist reserviertes T-SQL-Keyword; Identity-Basis liefert Passwort-Hash, Lockout, Stamps, 2FA-Felder |
| Spaltennamen `Row`/`Col` | `RowIdx` / `ColIdx` | `ROW`/`COL` sind T-SQL-Reserved |
| Cage-Modell | Relational (Cage + CageCell) | FK-Integrity, einfache Joins, prüfer-testbar via SQL |
| Solution-Speicherung | **NICHT** in DB | UC04 README: "No solution is recorded" |
| Highscore | DB-Read über LINQ-Joins (`Game ⋈ AppUser ⋈ Puzzle`) im `HighscoreService`; **View `vw_Highscore` existiert im Schema** ([`db/sudoku.sql`](../db/sudoku.sql)) als Reserve, wird vom aktuellen Service jedoch **nicht** gemappt | Service-Code referenziert `_db.Games` direkt; View-Switch bleibt offen, ohne dass das Schema angepasst werden muss |
| "Eine Zelle pro Cage in einem Puzzle" | Application-Validation (`PuzzleStructureValidator`) + DB-Trigger `trg_CageCell_UniquePerPuzzle` (AFTER INSERT/UPDATE) | Defense-in-Depth; Trigger lebt nur im SQL-Skript, nicht im EF-Modell |
| Game-Pause | `IsPaused` + `PausedAt` + `TotalPausedSeconds` | erlaubt sauberen Timer-Resume; `TimeSeconds = DATEDIFF(SECOND, StartTime, EndTime) − TotalPausedSeconds` |
| GameCell.CellValue | NULL erlaubt | leere Zelle vs eingetragener Wert |
| Game.Score / Game.TimeSeconds / Game.EndTime / Game.PausedAt | NULL erlaubt | Bei laufendem/pausiertem Spiel noch nicht gesetzt; Default-Zustand → `Score IS NULL` und `EndTime IS NULL` |
| PencilMark Modell | eigene Tabelle mit Composite PK | `(GameId, RowIdx, ColIdx, MarkValue)` — bis zu 9 Marks pro Zelle, garantiert ohne Duplikate |

## Constraints (zusammengefasst)

| Constraint | Layer | Beschreibung |
|------------|-------|--------------|
| `AppUser.UserName` UNIQUE (filtered, WHERE NOT NULL) | DB | Identity-Standard; Pflicht-Length 3–50 und Pattern werden in der App ([V01](validation.md#v01--username-uc02)) durchgesetzt — kein CHECK-Constraint im SQL |
| `AppUser.Email` UNIQUE (filtered, WHERE NOT NULL) | DB | Identity-Standard; Format-Validation läuft in der App ([V02](validation.md#v02--email-uc02)) — kein CHECK-Constraint im SQL |
| `AppUser.PasswordHash` | App | Pflicht-Hash via ASP.NET Identity (PBKDF2); Spalte selbst ist Identity-nullable |
| `Puzzle.Difficulty` CHECK ∈ {1, 2, 3} | DB | `CK_Puzzle_Difficulty` |
| `Cage.Sum` CHECK BETWEEN 1 AND 45 | DB | `CK_Cage_Sum_Range` |
| `CageCell.RowIdx` / `ColIdx` CHECK BETWEEN 0 AND 8 | DB | `CK_CageCell_RowRange` / `CK_CageCell_ColRange` |
| `GameCell.CellValue` CHECK NULL OR BETWEEN 1 AND 9 | DB | `CK_GameCell_Value` |
| `PencilMark.MarkValue` CHECK BETWEEN 1 AND 9 | DB | `CK_PencilMark_Value` |
| `Game.TimeSeconds` CHECK NULL OR ≥ 0 | DB | `CK_Game_TimeSeconds` |
| `Game.HintsUsed` CHECK ≥ 0 | DB | `CK_Game_HintsUsed` |
| `Game.Score` CHECK NULL OR ≥ 0 | DB | `CK_Game_Score` |
| `Game.TotalPausedSeconds` CHECK ≥ 0 | DB | `CK_Game_TotalPaused` |
| Filtered Unique Index `UX_Game_ActiveOnly` | DB | `(UserId, PuzzleId)` WHERE `IsCompleted = 0` — max. 1 aktives Game pro User/Puzzle |
| Trigger `trg_CageCell_UniquePerPuzzle` | DB (nur SQL-Skript, kein EF) | "Eine Zelle pro Puzzle in genau einem Cage" — siehe Anmerkung unten |
| Cage-Coverage (81 Zellen, jede Zelle ein Cage) | App | Wird in `PuzzleStructureValidator` (Core) geprüft; keine DB-Constraint |

> **Hinweis Trigger:** `trg_CageCell_UniquePerPuzzle` ist Teil von `db/sudoku.sql` und wirkt zur Laufzeit. Er ist nicht im `SudokuDbContext` modelliert; beim Anlegen des Schemas via EF-Migration müsste er separat hinzugefügt werden — aktuell wird das Schema ausschließlich über das SQL-Skript bereitgestellt.

## Ableitungen für Tests

- **UC04 → AC04.4:** Test "Puzzle-Tabelle hat KEINE Solution-Spalte" = SQL-Query auf `INFORMATION_SCHEMA.COLUMNS` (siehe `DbConstraintTests.T037`).
- **UC04 → AC04.1:** Test "INSERT mit Difficulty=4 wird abgelehnt" = SQL-Constraint-Test (`DbConstraintTests.Puzzle_Difficulty_OutOfRange_IsRejected`).
- **UC04 → AC04.3:** Test "INSERT Cage mit Sum=46 wird abgelehnt" = SQL-Constraint-Test.
- **UC04 → AC04.2:** Test "Eine Zelle in zwei Cages wird abgelehnt" verifiziert den Trigger (`DbConstraintTests.T040`).
- **UC05 → AC05.1:** Integration-Test: Save versuche mit unlösbarem Puzzle → kein Row in `Puzzle` (`PuzzleServiceTests.T042`).
- **UC13 → AC13.3:** UNIQUE-Index `UX_Game_ActiveOnly (UserId, PuzzleId)` WHERE `IsCompleted = 0` (Filtered Index) — verhindert mehrere offene Games pro User-Puzzle-Kombi; geprüft durch `DbConstraintTests.T109`.

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
