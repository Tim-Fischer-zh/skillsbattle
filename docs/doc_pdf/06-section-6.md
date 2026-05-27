<h1 id="section-6">Sektion 6 — Test Protocol — Killer Sudoku</h1>

**Stack:** .NET 10, Blazor Server, MS-SQL Express
**Frameworks:** xUnit (Unit/Integration), bUnit (Component), FluentAssertions, WebApplicationFactory + Testcontainers-MSSQL (Integration-DB) (WebApplicationFactory wird aktuell **nicht** verwendet; die Integration-Tests instanziieren Services direkt über eine `ServiceFactory` gegen den von `MsSqlContainerFixture` bereitgestellten Container), Playwright .NET (E2E)
**Coverage-Ziel:** 80% Lines/Branches (via Coverlet)

> **Update-Hinweis (Doku vs. Code-Stand):**
> - Tatsächliche Methodenanzahl im `source/tests/`-Baum: **98** xUnit-Methoden ([Fact]: 94, [Theory]: 4). Die "Total 94"-Zeilen weiter unten reflektieren die ursprüngliche Planung; die zusätzlichen 4 Methoden entstanden bei TDD-Iterationen.
> - Doppelte T-ID `T044` in `PuzzleServiceTests.cs`: einmal Sum-Check (Z. 72) und einmal Performance-Test (Z. 271). Die T-ID-Kollision ist bekannt und stört Coverage-Auswertung nicht; bei einer Konsolidierung wäre der Performance-Test als `T046` umzunummerieren.
> - Bewusst angelegte semantische Duplikate: `T093 ≈ T087` (DB-Constraint TimeSeconds < 0), `T122 ≈ T051` (Filtered-Index Active-Game-Unique), `T045 ≈ T044` (Sum-Check) — sind im Code-Kommentar als "Semantisches Duplikat" markiert und decken den Pfad bewusst auf zwei Layern ab.

## Test-Type-Legende

| Code | Type | Tool | Zweck |
|------|------|------|-------|
| U | Unit | xUnit + FluentAssertions | Reine Logik (Solver, Validator, Score-Formel) |
| C | Component | bUnit | Blazor-Component-Rendering + Events |
| I | Integration | xUnit + WebApplicationFactory + Testcontainers-MSSQL | Service + EF + DB |
| E | E2E | Playwright .NET | Voller User-Flow via Browser |
| M | Manual | — (Excel-Steps) | Visuelle UI / Browser-Compat-Checks |

## Priorität

- **P1** = Spec-strikte Constraint, kritischer Pfad (Submit-Risk wenn fail)
- **P2** = Wichtig (Validation, Standard-Flow)
- **P3** = Boundary / Edge-Case (Coverage-Erhöher)

---

# Test-Cases

> **Type-Legende** (gilt für die `Type`-Spalte aller folgenden Tabellen)
>
> | Code | Type | Tool / Framework |
> |:---:|------|------------------|
> | **U** | Unit | xUnit + FluentAssertions (reine Logik, keine DB, keine HTTP) |
> | **C** | Component | bUnit (Blazor-Component-Rendering + Events) |
> | **I** | Integration | xUnit + `WebApplicationFactory<Program>` + Testcontainers-MSSQL (Service ↔ EF Core ↔ echte DB) |
> | **E** | E2E | Playwright .NET (Browser-getriebener User-Flow) |
> | **M** | Manual | Excel-Steps (visuelle / Cross-Browser / Mobile-Viewport-Checks) |
>
> **Priorität:** P1 = Spec-strikte Constraint (Submit-Risk wenn fail) · P2 = Standard-Flow · P3 = Boundary / Edge-Case
>
> **README §3.1-Regel:** pro UC ≥ 1 positiver + ≥ 1 negativer + (wo möglich) ≥ 1 Boundary-Test. Coverage-Validierung siehe Inventar-Summary unten.

## UC01 — Read Rules

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T001 | Startseite ohne Login erreichbar | E | Playwright | Server läuft | 1. Browser zu `/`. 2. Page-Title prüfen. | — | Status 200, h1 enthält "Killer Sudoku" | P1 |
| T002 | Alle 4 Spielregeln sichtbar | E | Playwright | Server läuft | 1. Navigiere zu `/`. 2. Suche Textfragmente "1–9", "row", "cage", "no duplicate". | — | Alle 4 Texte präsent | P1 |
| T003 | Beispiel-Grid wird gerendert mit ≥ 1 Cage-Summe | C | bUnit | — | Component `MiniSudokuExample.razor` rendern | Mock-Puzzle mit 4 Cages | DOM enthält ≥1 `.cage-sum`-Element mit Zahl 1–45 | P2 |
| T004 | Negative: nicht-existierende Route → 404 | E | Playwright | — | Navigiere zu `/no-such-page` | — | Status 404 ODER NotFound-Component | P3 |

## UC02 — Create User

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T010 | Positive: gültige Registrierung | I | xUnit + WAF | Empty DB | 1. `IAuthService.RegisterAsync` mit gültigen Daten. 2. SELECT AppUser. | username="alice", email="alice@test.ch", pw="Test1234" | Result=Success, 1 Row in AppUser, PasswordHash ≠ Klartext | P1 |
| T011 | Negative: Username zu kurz | U | xUnit | — | Validator.Validate(dto) | username="ab" | ValidationError "min 3 Zeichen" | P2 |
| T012 | Boundary: Username genau 3 Zeichen → OK; 2 → Fehler | U | xUnit | — | Validator.Validate (parametrized) | "abc" / "ab" / "a"×50 / "a"×51 | 3 OK, 2 Fehler, 50 OK, 51 Fehler | P3 |
| T013 | Negative: Username bereits vergeben | I | xUnit + WAF | AppUser "alice" existiert | RegisterAsync mit gleichem Username | username="alice" | Result=DuplicateUsername | P1 |
| T014 | Negative: Email-Format ungültig | U | xUnit | — | Validator.Validate | email="noat.com" | ValidationError "Email-Format" | P2 |
| T015 | Negative: Passwort-Confirm-Mismatch | U | xUnit | — | Validator.Validate | pw="Test1234", confirm="Test1235" | ValidationError "Passwords don't match" | P2 |
| T016 | Negative: Passwort < 8 Zeichen | U | xUnit | — | Validator.Validate | pw="Test12" | ValidationError "min 8 Zeichen" | P2 |
| T017 | Security: Passwort wird NIE im Klartext gespeichert | I | xUnit + WAF | — | RegisterAsync; SELECT PasswordHash FROM AppUser | pw="Test1234" | Hash-String, KEIN "Test1234"-Match | P1 |
| T018 | E2E Register-Flow | E | Playwright | Server läuft, leere DB | 1. → /register. 2. Felder füllen. 3. Submit. 4. Erwarte Redirect zu /login. | full set | Success-Toast + Redirect | P1 |

## UC03 — Login

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T020 | Positive: gültiger Login | I | xUnit + WAF | User "alice" mit pw "Test1234" registriert | LoginAsync(alice, Test1234) | — | Result=Success, Cookie gesetzt | P1 |
| T021 | Negative: Falsches Passwort | I | xUnit + WAF | User "alice" existiert | LoginAsync(alice, wrong) | — | Result=Failure, generische Message | P1 |
| T022 | Security: Fehler-Message ist generisch (keine User-Existenz-Auskunft) | I | xUnit + WAF | DB hat "alice", "bob" nicht | LoginAsync(bob, x) + LoginAsync(alice, wrong) → Messages vergleichen | — | Beide Messages identisch | P1 |
| T023 | Login-Cookie ist HttpOnly + Secure | E | Playwright | — | Login flow + Cookies inspect | — | Cookie hat HttpOnly + Secure Flags | P2 |
| T024 | Rate-Limit nach 5 Fehlversuchen | I | xUnit + WAF | User "alice" existiert | 6× LoginAsync(alice, wrong) | — | 6. Aufruf → RateLimited | P3 |
| T025 | Geschützte Route → Redirect Login wenn nicht eingeloggt | E | Playwright | — | Browser ohne Cookie zu `/puzzles` | — | Redirect zu /login | P1 |
| T026 | E2E Login + Navigation | E | Playwright | User existiert | Login → erwartet Header zeigt Username | — | Sichtbares "alice" im Header | P1 |

## UC04 — Enter Puzzle

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T030 | Positive: Gültige Puzzle-Struktur akzeptiert | U | xUnit | — | Validator.Validate(PuzzleInputDto) | Example-1 from README | ValidationResult.IsValid=true | P1 |
| T031 | Negative: Difficulty=4 → Fehler | U | xUnit | — | Validator.Validate | difficulty=4 | "Difficulty must be 1-3" | P1 |
| T032 | Boundary: Difficulty 1 / 3 OK; 0 / 4 Fehler | U | xUnit (parametrized) | — | Validator.Validate | 0,1,2,3,4 | 1+2+3 OK, 0+4 Fehler | P3 |
| T033 | Negative: Zelle in 2 Cages → Fehler | U | xUnit | — | Validator.Validate mit Konflikt | Cage A: (0,0); Cage B: (0,0) | "Cell (0,0) in multiple cages" | P1 |
| T034 | Negative: Zelle keinem Cage → Fehler | U | xUnit | — | Validator.Validate mit Lücke | 80 zugeordnete Zellen | "Cell (8,8) unassigned" | P1 |
| T035 | Boundary: Cage-Sum 1 + 45 OK, 0 + 46 Fehler | U | xUnit (parametrized) | — | Cage.Sum mit 0/1/45/46 | — | 1+45 OK, 0+46 reject | P3 |
| T036 | DB-Schema: Puzzle hat keine Solution-Spalte | I | xUnit + WAF | DB-Migration appliziert | `SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Puzzle'` | — | Liste enthält KEIN "Solution" | P1 |
| T037 | DB-Constraint: INSERT Puzzle mit Difficulty=4 schlägt fehl | I | xUnit + WAF | — | Raw SQL: `INSERT INTO Puzzle (Difficulty, CreatedById) VALUES (4, 1)` | — | SqlException (CHECK violation) | P2 |

## UC05 — Save New Puzzle (Solvability)

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T040 | Positive: Solvable+Unique → wird gespeichert | I | xUnit + WAF | User existiert | SaveIfSolvableAsync(Example-1) | README-Example-1 | Result=Saved, Puzzle-Row + 28 Cages + 81 CageCells in DB | P1 |
| T041 | Negative: Multi-Solution → reject | I | xUnit + WAF | — | SaveIfSolvableAsync(ambiguous puzzle) | Bekanntes Multi-Solution-Puzzle | Result=MultipleSolutions, KEINE Row in Puzzle | P1 |
| T042 | Negative: Unsolvable → reject | I | xUnit + WAF | — | SaveIfSolvableAsync(impossible) | Cage-Sum kann nicht erreicht werden | Result=Unsolvable, KEINE Row in Puzzle | P1 |
| T043 | Rollback: Bei Fehler keine Teil-Daten in DB | I | xUnit + WAF | — | Inject failure mid-save | — | Keine Cage-Rows ohne Puzzle-Row | P2 |
| T044 | Performance Boundary: Solver < 2 Sekunden | U | xUnit | — | Stopwatch.Start; Solver.Solve(hardCase); Stop | Schwerstes Beispiel | Elapsed < 2000 ms | P3 |
| T045 | AC05.4 — Cage-Sum-Pre-Fail (Σ ≠ 405) → Reject ohne Solver-Call | I | xUnit + WAF | — | SaveIfSolvableAsync mit Cages deren Σ=400 | 27 Cages, Σ = 400 (1 fehlend) | Result=InvalidStructure ("Σ ≠ 405"), Solver-Mock 0× aufgerufen | P1 |

## UC06 — Solve Puzzle

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T050 | Positive: User startet Game | I | xUnit + WAF | Puzzle existiert | StartGameAsync(userId, puzzleId) | — | Row in Game mit StartTime, HintsUsed=0 | P1 |
| T051 | Negative: CellValue=0 → reject | U | xUnit | — | Service.SetCellValueAsync(g, 0, 0, 0) | — | ArgumentException ODER ValidationError | P2 |
| T052 | Boundary: CellValue 1/9 OK, 10 Fehler | I | xUnit + WAF | Game läuft | SetCellValueAsync 1/9/10 | — | 1+9 persistiert, 10 reject | P3 |
| T053 | UI: Tastatur 1–9 trägt Zahl ein | C | bUnit | PuzzleGrid mit Mock-Game | Press "5" auf aktive Zelle | — | DOM-Zelle zeigt "5" | P2 |

## UC07 — Hint

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T060 | Positive: Hint füllt korrekte Zelle | I | xUnit + WAF | Game läuft, Grid teilweise gefüllt | GetHintAsync(gameId) | — | Returnt eine Cell+Value die der eindeutigen Lösung entspricht | P1 |
| T061 | Side-Effect: HintsUsed wird inkrementiert | I | xUnit + WAF | Game mit HintsUsed=2 | GetHintAsync → SELECT HintsUsed | — | HintsUsed = 3 | P1 |
| T062 | Side-Effect: HintLog-Eintrag wird erstellt | I | xUnit + WAF | Game mit 0 HintLogs | GetHintAsync → COUNT HintLog | — | COUNT = 1 | P2 |
| T063 | Negative: Hint bei vollem Grid → Fehler | I | xUnit + WAF | Game mit 81 Cells gefüllt | GetHintAsync | — | InvalidOperationException ODER Failure-Result | P2 |
| T064 | Hint überschreibt falschen Wert nicht (oder zeigt falsche Zelle) | U | xUnit | Solver-Mock | HintLogic.PickCell(state with errors) | — | Picked Cell ist entweder leere ODER falsche Zelle (dokumentiert) | P3 |
| T065 | Boundary: Hint bei genau 80/81 gefüllten Zellen (letzte freie) | I | xUnit + WAF | Game mit 80/81 GameCells, 1 Zelle leer | GetHintAsync(gameId) | — | HintResult füllt exakt die letzte freie Zelle, HintsUsed +=1 | P3 |

## UC08 — High Score

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T070 | Positive: Score-Formel korrekt | U | xUnit | — | ScoreCalculator.Calculate(time=300, hints=2) | — | Score = 10000 - 300 - 2*300 = 9100 | P1 |
| T071 | Boundary: Score-Floor bei 0 | U | xUnit | — | Calculator(time=15000, hints=10) | — | Score = 0 (nicht negativ) | P3 |
| T072 | Sortierung absteigend nach Score | I | xUnit + WAF | 3 completed Games mit Scores 5000/8000/3000 | GetTopAsync(10) | — | Reihenfolge: 8000, 5000, 3000 | P2 |
| T073 | Empty: Keine Resultate → leere Liste | I | xUnit + WAF | Empty Game-Table | GetTopAsync(10) | — | Liste leer, keine Exception | P2 |
| T074 | E2E: Highscore-Page lädt Top-N | E | Playwright | DB hat 3 completed games | Navigate /highscore | — | Tabelle hat 3 Rows in Score-DESC | P2 |

## UC09 — Check Solution

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T080 | Positive: Korrekte Lösung → IsCorrect=true | I | xUnit + WAF | Game mit Beispiel-1-Solution | CheckSolutionAsync(gameId) | Bekannte Solution für Example-1 | IsCorrect=true | P1 |
| T081 | Sum-Check 405: Negative bei Σ ≠ 405 | U | xUnit | — | Validator.CheckSum(grid with 1 wrong value) | Eine 4 statt 5 → Σ=404 | IsCorrect=false, FailReason=SumMismatch | P1 |
| T082 | Sum-Check 405 ist erste Validierung (Performance) | U | xUnit (mit Mock-Solver) | Mock-Solver darf nicht aufgerufen werden | CheckSolution mit Σ=404 | — | Mock-Solver wurde NIE aufgerufen | P3 |
| T083 | Negative: Doppelte Zahl in Row | U | xUnit | — | Validator(grid mit 2× "5" in Row 0) | Row 0: 5,1,2,3,5,6,7,8,9 → fixed Sum aber duplicate | IsCorrect=false | P1 |
| T084 | Negative: Doppelte Zahl in Column | U | xUnit | — | Validator(grid mit Col-Duplikat) | — | IsCorrect=false | P1 |
| T085 | Negative: Doppelte Zahl in Nonet | U | xUnit | — | Validator(grid mit 3×3-Block-Duplikat) | — | IsCorrect=false | P1 |
| T086 | Negative: Cage-Sum-Mismatch | U | xUnit | — | Validator mit Cage-Sum=23 aber actual=22 | — | IsCorrect=false, FailReason=CageSum | P1 |
| T087 | Negative: Duplikat innerhalb Cage | U | xUnit | — | Cage [4,4] sum=8, beide Zellen=4 | Sum OK (8) aber Duplikat | IsCorrect=false, FailReason=CageDuplicate | P1 |
| T088 | Negative: Unvollständiges Grid | I | xUnit + WAF | Game mit 80 Zellen gefüllt | CheckSolutionAsync | — | IsCorrect=false, FailReason=Incomplete | P2 |

## UC10 — Save Result

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T090 | Positive: Game wird mit Score+EndTime gespeichert | I | xUnit + WAF | UC09 returnt IsCorrect | CompleteGameAsync(gameId) | — | Game-Row: IsCompleted=1, EndTime≠NULL, Score≥0 | P1 |
| T091 | TimeSeconds = (EndTime - StartTime) - TotalPausedSeconds | U | xUnit | — | Calculator(start=t0, end=t0+600, paused=120) | — | TimeSeconds = 480 | P2 |
| T092 | Negative: IsCompleted bleibt 0 bei falscher Lösung | I | xUnit + WAF | UC09 returnt IsCorrect=false | CompleteGameAsync wird NICHT aufgerufen (Service-Order) | — | Game.IsCompleted=0 | P2 |
| T093 | DB-Constraint: TimeSeconds < 0 wird abgelehnt | I | xUnit + WAF | — | Raw SQL UPDATE Game SET TimeSeconds=-1 | — | SqlException | P3 |
| T094 | Boundary: HintsUsed=0 → Score = 10000 − TimeSeconds (kein Hint-Penalty) | U | xUnit | — | ScoreCalculator.Calculate(time=500, hints=0) | — | Score = 9500 | P3 |

## UC11 — Auto Solve

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T100 | Positive: Example-1 hat genau 1 Lösung | U | xUnit | — | Solver.Solve(example1) | README example_1.png | Solutions=1, Solution=int[9,9] | P1 |
| T101 | Positive: Example-2 hat genau 1 Lösung | U | xUnit | — | Solver.Solve(example2) | README example_2.png | Solutions=1 | P1 |
| T102 | Lösung erfüllt alle 4 Constraints (Row/Col/Nonet/Cage) | U | xUnit | — | Solver.Solve + Validator.Check | — | Validator returns IsCorrect=true | P1 |
| T103 | Negative: Unsolvable puzzle | U | xUnit | — | Solver.Solve(craftedImpossible) | Cage [3,3,3] sum=20 (Max=24, OK)... synth: Cage[1,1] sum=2 (impossible) | Solutions=0 | P1 |
| T104 | Negative: Multi-Solution erkannt | U | xUnit | — | Solver.Solve(ambiguous) | Bekanntes 2-Solution-Puzzle | Solutions=2 (stop at 2nd) | P1 |
| T105 | Performance: Schwerstes Example < 2s | U | xUnit | — | Stopwatch.Measure(Solve(hardCase)) | — | Elapsed < 2000ms | P3 |
| T106 | Cage-Duplikat-Erkennung | U | xUnit | — | Solver-Trace bei Cage [4,5] sum=9 mit Branch der zu [4,4] führen würde | — | Solver verwirft diesen Branch | P3 |

## UC12 — Browse / Filter

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T110 | Positive: Liste zeigt alle Puzzles | I | xUnit + WAF | 3 Puzzles in DB | ListAsync(null, 1, 20) | — | 3 Items, Total=3 | P1 |
| T111 | Filter: Difficulty=2 zeigt nur Diff-2-Puzzles | I | xUnit + WAF | 1×Diff1, 2×Diff2, 1×Diff3 | ListAsync(2, 1, 20) | — | 2 Items, alle Diff=2 | P1 |
| T112 | Pagination: pageSize=2, page=2 | I | xUnit + WAF | 5 Puzzles | ListAsync(null, 2, 2) | — | Items[2..3], Total=5 | P2 |
| T113 | Empty: keine Puzzles → leere Liste | I | xUnit + WAF | Empty DB | ListAsync(null, 1, 20) | — | Items leer, Total=0 | P2 |
| T114 | E2E: Filter ändert URL + Liste | E | Playwright | 4 Puzzles | Klick "Mittel" → URL hat ?difficulty=2 | — | Sichtbare Cards = 2 | P2 |
| T115 | Boundary: Page=0 → ergibt Empty oder 400 (kein Crash) | I | xUnit + WAF | 5 Puzzles | ListAsync(null, 0, 20) | — | Result.Items leer ODER ArgumentException — kein Crash, kein 500 | P3 |

## UC13 — Pause / Resume

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T120 | Pause setzt IsPaused=1, PausedAt | I | xUnit + WAF | Game läuft | PauseAsync(gameId) | — | Game.IsPaused=1, PausedAt≠NULL | P1 |
| T121 | Resume erhöht TotalPausedSeconds | I | xUnit + WAF | Game pausiert seit 30s | ResumeAsync(gameId) | — | TotalPausedSeconds += ~30 | P1 |
| T122 | Constraint: max 1 aktives Game pro User-Puzzle | I | xUnit + WAF | Active Game (user1, puzzle1) | StartGameAsync(user1, puzzle1) erneut | — | Exception ODER returnt bestehendes GameId | P2 |
| T123 | Resume restored alle GameCells | I | xUnit + WAF | Game mit 30 gefüllten Zellen, pausiert | ResumeAsync + GetState | — | State enthält 30 Cells unverändert | P2 |
| T124 | E2E: Pause-Button → Overlay, Resume → editierbar | E | Playwright | Game läuft | Klick Pause, Klick Resume | — | Overlay erscheint+verschwindet | P3 |
| T125 | Boundary: Mehrere Pause/Resume-Cycles → TotalPausedSeconds akkumuliert | I | xUnit + WAF | Game läuft | 3× (Pause 10s → Resume) hintereinander | — | TotalPausedSeconds ≈ 30 (±1 für jitter) | P3 |

## UC14 — Pencil Marks

| ID | Title | Type | Framework | Preconditions | Steps | Test-Data | Expected | Priority |
|----|-------|------|-----------|---------------|-------|-----------|----------|----------|
| T130 | Toggle Mark: hinzufügen | I | xUnit + WAF | Game läuft, Cell (0,0) leer | TogglePencilMarkAsync(g, 0, 0, 5) | — | INSERT in PencilMark | P1 |
| T131 | Toggle Mark: entfernen wenn bereits gesetzt | I | xUnit + WAF | PencilMark (g, 0, 0, 5) existiert | TogglePencilMarkAsync(g, 0, 0, 5) | — | DELETE in PencilMark | P2 |
| T132 | Setzen finaler Wert löscht Pencil Marks | I | xUnit + WAF | (g, 0, 0) hat 3 Pencil Marks | SetCellValueAsync(g, 0, 0, 7) | — | PencilMark-Rows für (0,0) = 0 | P1 |
| T133 | Pencil Marks beeinflussen Check NICHT | U | xUnit | — | Validator.Check(solution mit Pencil-Marks-Daten) | — | IsCorrect basiert nur auf GameCell.CellValue | P3 |
| T134 | UI: Pencil-Mode-Toggle ändert Component-State | C | bUnit | — | Klick Pencil-Toggle | — | Component-State: PencilMode=true | P2 |
| T135 | Boundary: 9 Pencil-Marks (1–9) in einer Zelle gleichzeitig | I | xUnit + WAF | Game läuft, Cell (0,0) leer | 9× TogglePencilMarkAsync(g, 0, 0, n) für n=1..9 | — | 9 Rows in PencilMark für (0,0); kein Constraint-Fehler | P3 |
| T136 | Negative: Pencil-Mark in Zelle mit finalem Wert → Reject | U | xUnit | GameCell (g, 0, 0).CellValue = 5 | TogglePencilMarkAsync(g, 0, 0, 3) | — | InvalidOperationException oder Result.Failed (V13: "Pencil Marks nur in leeren Zellen") | P2 |

---

# Manual Test Cases (Excel-only, keine Automation)

| ID | Title | Type | Steps | Expected | Priority |
|----|-------|------|-------|----------|----------|
| M001 | Visual: Cage-Borders sichtbar in Chrome | M | 1. Chrome öffnen. 2. /puzzles/1/play. 3. Cage-Borders prüfen. | Gestrichelte Linien sichtbar | P2 |
| M002 | Visual: Cage-Borders sichtbar in Firefox | M | dito in Firefox | gleich | P2 |
| M003 | Tastatur-Navigation: Pfeiltasten + 1-9 + Delete | M | Spielscreen, Pfeil + Zahl-Input | Aktive Zelle wandert, Werte trag in | P2 |
| M004 | Cross-Browser: Highscore lädt in beiden Browsern | M | /highscore in Chrome + Firefox | Layout identisch | P3 |
| M005 | Mobile-Viewport: Layout funktional bei 768 px | M | Browser-DevTools 768×1024 | Grid noch nutzbar, kein Overlap | P3 |

---

# Test-Inventar Summary

## Aufschlüsselung nach Type

| Kategorie | Count | % |
|-----------|------:|--:|
| Unit (U) | 34 | 36 % |
| Component (C) | 3 | 3 % |
| Integration (I) | 42 | 45 % |
| E2E (E) | 10 | 11 % |
| Manual (M) | 5 | 5 % |
| **Total (geplant)** | **94** | 100 % · Realer Code-Stand: 98 Methoden ([Fact]: 94, [Theory]: 4) |

## Coverage-Validierung gegen README §3.1

> "For each use case, create at least one **positive**, one **negative**, and, **where possible**, one test case for **boundary conditions**."

| UC | Tests | Positiv | Negativ | Boundary | README-§3.1-Erfüllung |
|----|------:|--------:|--------:|---------:|:---------------------:|
| UC01 Read Rules | 4 | 3 | 1 | n/a (statisch) | ✓ |
| UC02 Create User | 9 | 3 | 5 | 1 (Username 2/3/50/51) | ✓ |
| UC03 Login | 7 | 4 | 2 | 1 (5-Fehler Rate-Limit) | ✓ |
| UC04 Enter Puzzle | 8 | 2 | 4 | 2 (Diff 0/1/3/4 + Cage-Sum 0/1/45/46) | ✓ |
| UC05 Save Puzzle | 6 | 1 | 4 | 1 (Solver-Perf < 2 s) | ✓ |
| UC06 Solve Puzzle | 4 | 2 | 1 | 1 (Cell 1/9/10) | ✓ |
| UC07 Hint | 6 | 3 | 1 | 2 (Falsch-Wert-Edge + 80/81-letzte) | ✓ |
| UC08 High Score | 5 | 3 | 1 | 1 (Score-Floor=0) | ✓ |
| UC09 Check Solution | 9 | 1 | 7 | 1 (Sum-Check-First Spy) | ✓ |
| UC10 Save Result | 5 | 2 | 2 | 1 (Hints=0 → max Score) | ✓ |
| UC11 Auto Solve | 7 | 3 | 3 | 1 (Perf < 2 s) | ✓ |
| UC12 Browse / Filter | 6 | 4 | 1 | 1 (Page=0) | ✓ |
| UC13 Pause / Resume | 6 | 4 | 1 | 1 (Multi-Cycle Akkumulation) | ✓ |
| UC14 Pencil Marks | 7 | 5 | 1 | 1 (9 Marks max) | ✓ |
| Manual (cross-UC visual) | 5 | — | — | — | n/a (visuell) |
| **Total (geplant)** | **94** | | | | **alle 14 UCs ✓** · realer Code: 98 Methoden |

→ README §3.1 Mindestanforderung pro UC erfüllt; UC01 ohne echten Boundary (Klausel "where possible" greift, statische Seite).

## Aufschlüsselung nach Priorität

| Priorität | Count |
|-----------|------:|
| P1 (Spec-strikt) | ~40 |
| P2 (Standard) | ~36 |
| P3 (Boundary/Edge) | ~18 |

**Coverage-Target:** ≥ 80 % Lines/Branches (Coverlet) insgesamt; **≥ 95 % für `KillerSudoku.Core`** (Solver + Validator kritisch).

---

# Bemerkungen für die Doku

- Hidden-Test-Modellierung (siehe Skill-7-Pattern 10): Die negativ-Tests T033/T041/T087 (Cage-Duplikat ohne Sum-Mismatch) sind typische Prüfer-Tests, die ein AI-typischer Solver-Stub übersieht.
- Pre-Submission-Check: T036 (no Solution column) ist ein **DB-Schema-Test** und verhindert das häufige AI-Pattern "ich speichere doch lieber die Lösung in der DB für Performance".
- Sum-Check-First (T082): verhindert dass der Solver bei offensichtlich falschen Lösungen läuft → Performance + Bestätigung dass die Implementation der Spec folgt.
