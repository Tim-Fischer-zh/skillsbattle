<h1 id="chapter-6">6 Laufzeitsicht</h1>

> arc42 v8.2 · Kapitel 6 · Killer Sudoku
> Bezug: [Kapitel 5](#chapter-5) · **Use-Cases-Dokument** · **Funktionalitäts-Matrix** · **Validation-Regeln** · **ER-Modell**

Dieses Kapitel beschreibt die wichtigsten **Laufzeit-Szenarien** als Sequenz-Diagramme. Service-Methoden-Namen entsprechen exakt dem Service-Interface-Block in **Funktionalitäts-Matrix** §Service-Interfaces. DB-Tabellen sind in **Datenbank-Skript** belegt.

**Auswahl der Szenarien:** Die vier dargestellten Use-Case-Flows decken die kritischen Architektur-Pfade ab — Save-with-Solver, Hint-Generation, Solution-Check (inkl. Sum-Fast-Fail) und Pause/Resume-State-Persistierung. Diese Pfade involvieren alle drei Layer + den Domain-Kern.

---

## 6.1 Szenario A — Puzzle anlegen und speichern (UC04 + UC05)

**Ziel:** User erstellt ein neues Killer-Sudoku, Server validiert Struktur, ruft Solver für Eindeutigkeits-Check, persistiert nur bei genau einer Lösung. Wörtliche README-Grundlage: *"The puzzle can only be saved if it is solvable"* (UC05) und *"The solution must be unique"* (§1).

**Beteiligt:** UC04 + UC05 · **V05** · **V06** · **V07** · Screen S5.

```mermaid
sequenceDiagram
 autonumber
 actor User
 participant UI as EnterPuzzle.razor<br/>+ PuzzleGrid + CageEditor
 participant PSvc as IPuzzleService
 participant SSvc as ISolverService
 participant Db as SudokuDbContext<br/>(MS-SQL)

 User->>UI: Trägt Givens ein, definiert Cages,<br/>wählt Difficulty (1/2/3)
 User->>UI: Klick "Speichern"
 UI->>PSvc: ValidateStructureAsync(PuzzleInputDto)
 PSvc-->>UI: ValidationResult (OK | Errors)

 alt Struktur ungültig (V05/V06)
 UI-->>User: Fehler anzeigen<br/>(Eingaben bleiben erhalten — AC05.3)
 else Struktur OK
 UI->>PSvc: SaveIfSolvableAsync(PuzzleInputDto, userId)
 PSvc->>SSvc: CountSolutions(givenValues, cages)
 Note over SSvc: Backtracking +<br/>Constraint-Propagation<br/>(Row/Col/Nonet + Cage-Sum + No-Dup-in-Cage)<br/>Abbruch nach 2. Lösung (AC11.2)
 SSvc-->>PSvc: 0 | 1 | 2

 alt Solutions == 0 (V07)
 PSvc-->>UI: SavePuzzleResult: Reject<br/>"Puzzle ist nicht lösbar."
 UI-->>User: Fehler-Toast
 else Solutions ≥ 2 (V07 strict)
 PSvc-->>UI: SavePuzzleResult: Reject<br/>"Mehr als eine Lösung."
 UI-->>User: Fehler-Toast
 else Solutions == 1
 PSvc->>Db: BEGIN TRANSACTION
 PSvc->>Db: INSERT Puzzle(Difficulty, CreatedById)
 Db-->>PSvc: Puzzle.Id
 loop für jeden Cage in Cages
 PSvc->>Db: INSERT Cage(PuzzleId, Sum)
 Db-->>PSvc: Cage.Id
 loop für jede Cell im Cage
 PSvc->>Db: INSERT CageCell(CageId, RowIdx, ColIdx)
 Note over Db: Trigger trg_CageCell_UniquePerPuzzle<br/>prüft "Cell in 1 Cage pro Puzzle"
 end
 end
 PSvc->>Db: COMMIT
 PSvc-->>UI: SavePuzzleResult: Ok(PuzzleId)
 UI-->>User: Bestätigung + Redirect /puzzles
 end
 end
```

**Wichtige Architektur-Eigenschaften:**

- **Transaktionsklammer** liegt in `IPuzzleService.SaveIfSolvableAsync`, nicht in der UI.
- **Solver wird VOR jedem INSERT** ausgeführt — keine "Roll-Back-on-Failure"-Konstruktion nötig.
- **Keine Solution-Speicherung** — UC04 wörtlich: *"No solution is recorded"*; die Cage-Tabelle reicht für späteres Re-Solving.
- **DB-Trigger** ist Last-Line-of-Defense — falls die Application-Validierung Bugs hat, schützt der Trigger Datenintegrität (Defense-in-Depth).

---

## 6.2 Szenario B — Hint anfordern (UC07)

**Ziel:** User klickt "Hint", System bestimmt aus aktuellem Spielzustand eine Zelle + den korrekten Wert, persistiert Hint-Audit-Eintrag, inkrementiert `HintsUsed` (Score-relevant via UC08).

**Beteiligt:** UC07 · **V11** · Screen S6 · `IHintService` + `ISolverService`.

```mermaid
sequenceDiagram
 autonumber
 actor User
 participant UI as HintButton.razor<br/>+ PuzzleGrid
 participant GSvc as IGameService
 participant HSvc as IHintService
 participant SSvc as ISolverService
 participant Db as SudokuDbContext<br/>(MS-SQL)

 User->>UI: Klick "Hint"
 Note over UI: Client: V11 — Button disabled<br/>wenn Grid voll
 UI->>HSvc: GetHintAsync(gameId)
 HSvc->>Db: SELECT GameCell WHERE GameId=@id<br/>SELECT Cage + CageCell WHERE PuzzleId=...
 Db-->>HSvc: currentBoard[9,9] + cages

 alt Grid bereits vollständig (Server-Check V11)
 HSvc-->>UI: Error "Grid bereits voll"
 else Grid unvollständig
 HSvc->>SSvc: Solve(givens, cages)
 SSvc-->>HSvc: SolveResult { Solutions: 1, Solution[,] }

 Note over HSvc: Hint-Auswahl-Strategie:<br/>1. Naked Single (UC07 Strategy A)<br/>2. Cage-Forced (UC07 Strategy B)<br/>3. Fallback: erste leere Zelle aus Solution
 HSvc->>HSvc: chooseHintCell<br/>→ (row, col, value)

 HSvc->>Db: BEGIN TRANSACTION
 HSvc->>Db: UPDATE GameCell<br/>SET CellValue=@value<br/>WHERE GameId=@id AND RowIdx=@r AND ColIdx=@c
 HSvc->>Db: UPDATE Game SET HintsUsed = HintsUsed + 1<br/>WHERE Id=@gameId
 HSvc->>Db: INSERT HintLog(GameId, RowIdx, ColIdx, HintAt)
 HSvc->>Db: COMMIT

 HSvc-->>UI: HintResult { RowIdx, ColIdx, Value }
 UI->>UI: Zelle in PuzzleGrid hervorheben<br/>+ Wert eintragen
 UI-->>User: Visuelles Feedback
 end
```

**Wichtige Architektur-Eigenschaften:**

- **Solver wird stateless aufgerufen** — er bekommt den aktuellen Board-Zustand inkl. User-Eingaben; durch AC11.1 (Solver erkennt 0/1/≥2 korrekt) ist garantiert, dass das gespeicherte Puzzle eine eindeutige Lösung hat, also `Solutions == 1` für vorherige Hint-Anfragen sicher ist (solange User-Eingaben nicht widersprüchlich sind — siehe UC07 AC07.3).
- **HintsUsed** wird atomar mit dem `GameCell`-Update und dem `HintLog`-INSERT in einer Transaktion erhöht — Audit + Score-Input bleiben konsistent.
- **Score-Wirkung:** Jeder Hint kostet 300 Punkte (Formel UC08 §High Score) — daher die Audit-Disziplin.

---

## 6.3 Szenario C — Lösung prüfen (UC09)

**Ziel:** User triggert Lösungsprüfung; System validiert in **drei Stufen**, sortiert nach Performance: (1) Sum-Check 405 als O(1)-Fast-Fail, (2) Row/Col/Nonet-Check, (3) Cage-Sum + Cage-No-Duplicate. README §2.3 wörtlich: *"Use the value for a 'simple' validation before checking the solution with an algorithm"*.

**Beteiligt:** UC09 · **V09** · **V10** · Screen S6 · `IGameService`.

```mermaid
sequenceDiagram
 autonumber
 actor User
 participant UI as CheckSolutionButton<br/>+ PuzzleGrid
 participant GSvc as IGameService
 participant Val as SolutionValidator<br/>(Domain-Kern)
 participant Db as SudokuDbContext<br/>(MS-SQL)

 User->>UI: Klick "Lösung prüfen"<br/>oder Auto-Trigger (alle 81 Zellen voll)
 UI->>GSvc: CheckSolutionAsync(gameId)
 GSvc->>Db: SELECT GameCell WHERE GameId=@id
 Db-->>GSvc: cellValues (81 Werte)

 GSvc->>GSvc: Vollständig befüllt?<br/>(81 Zellen, alle ≠ NULL)

 alt Nicht vollständig
 GSvc-->>UI: CheckResult { IsCorrect: false }<br/>"Bitte zuerst alle Felder ausfüllen"
 UI-->>User: Fehler-Toast
 else Vollständig
 Note over GSvc: ── Stufe 1: Sum-Fast-Fail (V09) ──<br/>SUM(CellValue) == 405 ?<br/>(9 Reihen × 45 = 405)
 GSvc->>GSvc: total = sum(cellValues)

 alt total ≠ 405
 GSvc-->>UI: CheckResult { IsCorrect: false }<br/>"Lösung falsch"
 Note over GSvc: KEIN Validator-Aufruf (AC09.1)
 else total == 405
 Note over GSvc: ── Stufe 2: Sudoku-Regeln (V10) ──
 GSvc->>Val: ValidateRowsColsNonets(cellValues)
 Val-->>GSvc: rowsColsNonetsOk: bool

 alt Sudoku-Regel verletzt
 GSvc-->>UI: CheckResult { IsCorrect: false }
 else Sudoku-Regeln OK
 Note over GSvc: ── Stufe 3: Cage-Regeln (V10) ──<br/>Cage-Sum match + No-Dup-in-Cage<br/>(Beispiel-SQL siehe ../erm.md)
 GSvc->>Db: SELECT Cage + CageCell für Puzzle
 Db-->>GSvc: cages
 GSvc->>Val: ValidateCages(cellValues, cages)
 Val-->>GSvc: cagesOk: bool

 alt Cage-Regel verletzt
 GSvc-->>UI: CheckResult { IsCorrect: false }
 else Alle Stufen OK
 Note over GSvc: → Auto-Trigger UC10 (CompleteGameAsync)
 GSvc-->>UI: CheckResult { IsCorrect: true }
 UI-->>User: "Glückwunsch! Score: N"<br/>(siehe Szenario UC10)
 end
 end
 end
 end
```

**Performance-Begründung (in Reihenfolge der drei Stufen):**

1. **Sum-Check 405** — O(81) Addition, kein Allokation. Filtert grobe Fehler (falsche Zahl, vergessene Zelle) sofort.
2. **Row/Col/Nonet** — 27 × 9-Element-Distinct-Check, O(243) ohne Set-Allokation via Bitmask.
3. **Cage-Sum + No-Duplicate** — Pro Cage einmal `cellValues` durchgehen, Set-basierter Duplikat-Check. Komplexität proportional zur Anzahl Cages × Cage-Größe ≤ 81 Operationen.

Die Sum-Check-Reihenfolge ist nicht Optimierung um der Optimierung willen — sie ist **wörtliche Anforderung** aus README §2.3 und Pattern für **alle** "simple before algorithmic" Validierungen im System.

---

## 6.4 Szenario D — Pause und Resume (UC13)

**Ziel:** User pausiert ein laufendes Spiel; Server friert Timer ein und persistiert den Zustand; bei Resume wird der Timer um die Pausen-Dauer korrigiert weitergeführt. Formel aus **ER-Modell**: `TimeSeconds = DATEDIFF(SECOND, StartTime, EndTime) − TotalPausedSeconds`.

**Beteiligt:** UC13 · **V12** · Screen S6 · `IGameService.PauseAsync`/`ResumeAsync`.

```mermaid
sequenceDiagram
 autonumber
 actor User
 participant UI as PauseButton.razor<br/>+ PlayPuzzle
 participant GSvc as IGameService
 participant Db as SudokuDbContext<br/>(MS-SQL)

 Note over User,Db: ── Pause-Flow ──
 User->>UI: Klick "Pause"
 UI->>GSvc: PauseAsync(gameId)

 GSvc->>Db: SELECT Game WHERE Id=@id
 Db-->>GSvc: Game (IsPaused, PausedAt, TotalPausedSeconds, UserId)

 alt UserId ≠ currentUserId (V16)
 GSvc-->>UI: 403 Forbidden
 else IsPaused == 1 (bereits pausiert)
 GSvc-->>UI: no-op
 else
 GSvc->>Db: UPDATE Game<br/>SET IsPaused=1, PausedAt=SYSUTCDATETIME<br/>WHERE Id=@id
 GSvc-->>UI: Ok
 UI->>UI: Timer einfrieren,<br/>Grid disablen<br/>"Pausiert" anzeigen
 end

 Note over User,Db: ── (Zeit vergeht — ggf. Logout/Browser-Close) ──

 Note over User,Db: ── Resume-Flow ──
 User->>UI: Klick "Weiterspielen"
 UI->>GSvc: ResumeAsync(gameId)

 GSvc->>Db: SELECT Game WHERE Id=@id
 Db-->>GSvc: Game (IsPaused=1, PausedAt=t_pause)

 alt UserId ≠ currentUserId (V16)
 GSvc-->>UI: 403 Forbidden
 else IsPaused == 0 (nicht pausiert)
 GSvc-->>UI: no-op
 else
 Note over GSvc: pausedSpan = SYSUTCDATETIME − PausedAt
 GSvc->>Db: UPDATE Game<br/>SET IsPaused=0,<br/> PausedAt=NULL,<br/> TotalPausedSeconds = TotalPausedSeconds + pausedSpan<br/>WHERE Id=@id

 GSvc->>Db: SELECT GameCell, PencilMark<br/>WHERE GameId=@id
 Db-->>GSvc: Saved cells + pencil marks

 GSvc-->>UI: Restored state (cells + marks)
 UI->>UI: Timer fortsetzen,<br/>Grid editierbar machen<br/>Cells + Pencil Marks rendern (AC13.2)
 UI-->>User: Spiel weiterführen
 end
```

**Wichtige Architektur-Eigenschaften:**

- **TotalPausedSeconds** wird inkrementell aufaddiert — mehrere Pause/Resume-Zyklen sind dadurch sauber unterstützt.
- **Timer-Berechnung** bleibt in der DB-Formel verankert (siehe Comment-Block in **Datenbank-Skript** Zeilen 125–127): bei Spiel-Abschluss (UC10) gilt `TimeSeconds = DATEDIFF(SECOND, StartTime, EndTime) − TotalPausedSeconds`.
- **Filtered Unique Index** `UX_Game_ActiveOnly` (**ER-Modell**) garantiert AC13.3: ein User kann pro Puzzle nur **ein** nicht abgeschlossenes Game haben — verhindert Pause-im-Spiel-1 + Neu-Start-Spiel-2-vor-Resume-Doubles.
- **Auto-Save bei jedem Move** (UC13 Alt-Flow "Browser-Tab schliessen ohne Pause"): `IGameService.SetCellValueAsync` persistiert jeden Zell-Change sofort — daher ist auch ein impliziter "Pause durch Wegnavigation" verlustfrei.
- **Authorization** (**V16**): jede Mutation prüft `Game.UserId == currentUserId` und antwortet mit 403 bei Mismatch.

---

> **Nächstes Kapitel:** [Kapitel 7 — Verteilungssicht](#chapter-7) (TBD) — Deployment-Diagramm: lokale Dev-Workstation, MS-SQL-Express-Instanz, Build-/Run-Konfiguration.
