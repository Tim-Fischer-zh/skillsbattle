<h1 id="chapter-9">9 Architekturentscheidungen (ADRs)</h1>

> arc42 v8.2 · Skills Battle 2026 — Application Development — Killer Sudoku
> Quelldokument (autoritativ): **Aufgabenstellung**

Dieses Kapitel dokumentiert die wesentlichen Architekturentscheidungen als ADRs (Architecture Decision Records). Jeder Eintrag folgt dem Format:

- **Titel** — Was wurde entschieden
- **Status** — `Akzeptiert` (alle hier dokumentierten ADRs sind angenommen)
- **Kontext** — Welches Problem / welche Anforderung führt zur Entscheidung
- **Entscheidung** — Was wurde konkret gewählt
- **Konsequenz** — Welche positiven & negativen Folgen ergeben sich

---

## ADR-001 — Stack: .NET 10 + Blazor Server + MS-SQL Server

**Status:** Akzeptiert

**Kontext:**
Die Aufgabenstellung (Skills Battle 2026 — Skill 7) erlaubt verschiedene Implementations-Stacks (PHP, Node.js, .NET). README §1.3 fordert lediglich: "create a database named 'sudoku' on **MySQL or MS-SQL** server running on your machine." Die UI-Technologie ist frei wählbar.

**Entscheidung:**
Stack: **.NET 10** (Blazor Server) als Anwendungs-Framework und **MS-SQL Server Express** als Datenbank.

**Konsequenz:**
- (+) Strenges Typsystem (C# 13) reduziert Fehler bei Solver- und Domain-Logik
- (+) Blazor Server: vollständiges C#-Stack (Frontend + Backend), keine Sprach-Wechsel zwischen Layern
- (+) ASP.NET Core Identity bietet hardened-Auth out-of-the-box (siehe [§8.1](#chapter-8))
- (+) EF Core 10 + MS-SQL Express: konsistentes Tooling, Trusted Connection für lokales Setup
- (+) bUnit-Component-Tests + xUnit Unit-Tests sind im selben Test-Projekt möglich
- (−) Blazor Server benötigt permanente SignalR-Verbindung (siehe [§7.1](#chapter-7)) — bei Disconnect Verlust des UI-State (mitigiert via Auto-Save in DB, **UC13**)
- (−) .NET-Setup auf Prüfer-Laptop muss vorhanden sein (Mitigation: Submission liefert Build-Output in `bin/Release/net10.0/`, siehe [§7.4](#chapter-7))

---

## ADR-002 — Cage-Modellierung relational (Cage + CageCell)

**Status:** Akzeptiert

**Kontext:**
Ein Killer-Sudoku besteht aus Cages: Gruppen zusammenhängender Zellen mit einer Soll-Summe. Pro Puzzle gibt es 1–N Cages, jeder Cage hat 1–9 Zellen. Eine Zelle gehört genau einem Cage an. Modellierungs-Optionen:

1. **Relational** — Tabelle `Cage` (Id, PuzzleId, Sum) + Junction-Tabelle `CageCell` (CageId, RowIdx, ColIdx)
2. **JSON in Puzzle** — Eine Spalte `CagesJson NVARCHAR(MAX)` mit Array von Cages

**Entscheidung:**
**Variante 1 (relational).** Siehe Schema in **Datenbank-Skript** (Zeilen 67–96).

**Konsequenz:**
- (+) FK-Integrität & DB-CHECK-Constraints (Sum ∈ [1, 45], Row/Col ∈ [0, 8]) — siehe **V06**
- (+) Einfache SQL-Queries für Cage-Sum-Validation (UC09) — siehe Sample-Queries in **ER-Modell**
- (+) Indexierbarkeit (Index `IX_Cage_PuzzleId`)
- (+) Trigger `trg_CageCell_UniquePerPuzzle` lässt sich nur in der relationalen Variante sinnvoll umsetzen
- (−) Mehr Joins beim Lesen eines Puzzles (zwei Queries: Cages + CageCells, oder ein Join)
- (−) Speicher-Overhead minimal (TINYINT-Spalten + Composite PKs)

---

## ADR-003 — Highscore als View statt Tabelle

**Status:** Akzeptiert

**Kontext:**
UC08 (Show High Score) zeigt die Top-N abgeschlossenen Games sortiert nach Score. Optionen:

1. **Denormalisierte Tabelle** `Highscore` mit Sync-Logik bei UC10 (Save Result)
2. **View** `vw_Highscore` über `Game JOIN AppUser JOIN Puzzle`

**Entscheidung:**
**Variante 2 (View).** Siehe **Datenbank-Skript** Zeilen 214–232.

**Konsequenz:**
- (+) Single Source of Truth: kein Sync-Risiko zwischen `Game` und `Highscore`
- (+) Automatisch konsistent bei UPDATE auf `Game`
- (+) `WHERE g.IsCompleted = 1 AND g.Score IS NOT NULL` stellt sicher, dass nur abgeschlossene Spiele erscheinen — passt zu UC10 AC10.3
- (−) Performance: bei sehr vielen Games (>10⁶) wäre View langsamer als materialisierte Tabelle. Für die Wettbewerbs-Submission (lokales Setup, wenige Spiele) irrelevant.
- (−) Pagination der View erfordert `OFFSET/FETCH` in jeder Abfrage statt vorberechneter Sortierung
- **Implementierungs-Realität:** Der `HighscoreService` liest aktuell **nicht** über die View, sondern direkt über LINQ-Joins (`_db.Games ⋈ AppUser ⋈ Puzzle WHERE IsCompleted = 1`). Der `SudokuDbContext` mappt `vw_Highscore` nicht als Keyless-Entity. Die View bleibt im SQL-Schema als Reserve — ein späterer Wechsel auf `FromSqlRaw("SELECT … FROM vw_Highscore")` oder einen Keyless-DbSet ist möglich, ohne dass das Schema geändert werden muss.
- Trade-Off-Begründung: LINQ erlaubt typsicheren Difficulty-Filter (`GetTopAsync(limit, byte? difficulty)`), Order-Stabilität (`OrderByDescending(Score).ThenBy(TimeSeconds)`) und Rank-Vergabe direkt im C#-Code. Bei wachsender Datenmenge oder Bedarf an View-Wartbarkeit ist Re-Mapping auf `vw_Highscore` der nächste sinnvolle Schritt.

---

## ADR-004 — "Eine Zelle pro Cage pro Puzzle" via Trigger

**Status:** Akzeptiert

**Kontext:**
Die Regel "jede Zelle gehört zu **genau einem** Cage pro Puzzle" (siehe UC04 AC04.2 und **V06**) lässt sich auf Datenbank-Ebene nicht direkt mit einer einzelnen UNIQUE-Constraint ausdrücken, weil `CageCell` kein direktes `PuzzleId`-Feld hat (PuzzleId steckt in `Cage`).

Optionen:

1. **Composite FK über (PuzzleId, RowIdx, ColIdx)** — würde `PuzzleId` zusätzlich in `CageCell` duplizieren
2. **AFTER INSERT/UPDATE Trigger** — prüft Cross-Cage-Konflikt im Puzzle
3. **Nur Application-Layer-Validierung** — kein DB-Schutz

**Entscheidung:**
**Variante 2 (Trigger).** Konkret `trg_CageCell_UniquePerPuzzle` (siehe **Datenbank-Skript** Zeilen 100–121):

```sql
CREATE OR ALTER TRIGGER trg_CageCell_UniquePerPuzzle
ON CageCell
AFTER INSERT, UPDATE
AS
BEGIN
 ...
 IF EXISTS (
 SELECT 1
 FROM CageCell cc
 JOIN Cage c1 ON c1.Id = cc.CageId
 JOIN inserted i ON i.RowIdx = cc.RowIdx AND i.ColIdx = cc.ColIdx
 JOIN Cage c2 ON c2.Id = i.CageId
 WHERE c1.PuzzleId = c2.PuzzleId
 AND cc.CageId <> i.CageId
 )
 BEGIN
 ROLLBACK TRANSACTION;
 THROW 50001, 'CageCell: Cell already assigned to another cage in the same puzzle.', 1;
 END
END;
```

**Konsequenz:**
- (+) Defense-in-Depth: Application-Validierung (V06) + DB-Trigger
- (+) Schema-Normalisierung erhalten — kein redundantes `PuzzleId` in `CageCell`
- (+) Klare Fehlermeldung im Konflikt-Fall (`THROW 50001`)
- (−) Trigger-Logik ist weniger sichtbar als deklarative Constraints
- (−) Performance-Overhead bei Bulk-Inserts (eher irrelevant: 81 Zellen pro Puzzle)
- Begründung im Kommentar des SQL-Skripts (Zeile 84–86): "Constraint 'jede Zelle pro Puzzle in genau einem Cage' wird über einen INSTEAD-OF-Trigger durchgesetzt"

> **Anmerkung zum SQL-Kommentar:** Der Kommentar im Skript spricht von "INSTEAD-OF-Trigger", umgesetzt wird tatsächlich ein `AFTER INSERT, UPDATE` Trigger. Funktional identisch (Rollback bei Konflikt), aber Wording im Kommentar bleibt zur Konsistenz mit dem Submission-File.

> **Anmerkung zum EF-Modell:** Der Trigger `trg_CageCell_UniquePerPuzzle` lebt ausschließlich im SQL-Skript `db/sudoku.sql`. Er ist im `SudokuDbContext` (`OnModelCreating`) **nicht** als HasTrigger-Konfiguration oder Migration-Operation modelliert. Konsequenz: Wer die Datenbank ausschließlich über `EnsureCreated` oder eine künftige EF-Migration aufsetzt, würde den Trigger nicht erhalten. Die aktuelle Submission setzt das Schema immer über **Datenbank-Skript** (Container-Entrypoint oder manueller `sqlcmd`-Aufruf), daher ist der Trigger im Lauf garantiert.

---

## ADR-005 — AppUser statt User (T-SQL Reserved Keyword)

**Status:** Akzeptiert

**Kontext:**
Die Benutzer-Tabelle würde idiomatisch `User` heissen. In T-SQL ist `USER` jedoch ein reserviertes Keyword (für `USER_NAME` etc.), was sowohl im Schema-Skript als auch in EF-LINQ-Queries ständige Eckige-Klammern-Quotation erfordern würde.

**Entscheidung:**
Tabelle und C#-Entity heissen **`AppUser`**. Siehe **ER-Modell** und **Datenbank-Skript** Zeilen 36–48.

**Konsequenz:**
- (+) Kein Quoting notwendig, klare Lesbarkeit in SQL und C#
- (+) Konvention findet sich in vielen ASP.NET-Identity-Templates (`AspNetUsers`, `ApplicationUser` etc.)
- (−) Leichte Reibung mit dem Use-Case-Vokabular ("User" als Begriff bleibt im Sprachgebrauch)

---

## ADR-006 — RowIdx / ColIdx statt Row / Col

**Status:** Akzeptiert

**Kontext:**
Sudoku-Zellen werden durch Zeilen- und Spalten-Index identifiziert. Idiomatisch wären die Spalten-Namen `Row` und `Col`. T-SQL reserviert allerdings `ROW` (z.B. in `ROW_NUMBER` und Table-Hints).

**Entscheidung:**
Spalten heissen **`RowIdx`** und **`ColIdx`** (TINYINT, Range 0–8). Siehe **ER-Modell** und **Datenbank-Skript**.

**Konsequenz:**
- (+) Kein Quoting in SQL
- (+) Suffix `Idx` macht den Zero-Based-Index explizit (nicht 1-Based wie in der UI)
- (−) Etwas länger als `Row`/`Col` — minimaler Cosmetic-Impact

---

## ADR-007 — KEINE Solution-Spalte in Puzzle

**Status:** Akzeptiert

**Kontext:**
Ein Puzzle hat genau eine eindeutige Lösung (gefordert durch README §1 "The solution **must be unique**." und durchgesetzt durch UC05). Naheliegende Optimierung wäre, die berechnete Lösung beim Save zu cachen.

Die Aufgabenstellung verbietet das jedoch explizit. README §2.1 UC4 wörtlich:
> "**No solution is recorded.** Solutions must be calculated with an algorithm."

**Entscheidung:**
Die Tabelle `Puzzle` enthält **keine** Solution-Spalte. Lösungen werden bei jedem Bedarf (UC05, UC07, UC09) on-the-fly via `ISolverService.Solve` berechnet.

**Konsequenz:**
- (+) Konform zur strikten README-Vorgabe — bestehensrelevant
- (+) Single Source of Truth: Solver ist die Autorität, kein DB-Cache kann veralten
- (+) Test: SQL-Query auf `INFORMATION_SCHEMA.COLUMNS` zeigt keine `Solution`-Spalte (UC04 AC04.4)
- (−) Solver wird mehrfach aufgerufen pro Game-Session (z.B. mehrere Hints) — mitigiert durch In-Memory-Cache pro Circuit, **niemals** persistent
- Konsequenz aus Strict-Word-Audit S4 in **Use-Cases-Dokument**

---

## ADR-008 — Pencil Marks als eigene Tabelle (statt JSON in GameCell)

**Status:** Akzeptiert

**Kontext:**
UC14 (Pencil Marks) erlaubt 0–9 Kandidaten-Markierungen pro Zelle. Modellierungs-Optionen:

1. **Eigene Tabelle** `PencilMark` mit Composite PK `(GameId, RowIdx, ColIdx, MarkValue)`
2. **JSON-Spalte** in `GameCell` (z.B. `PencilMarksJson NVARCHAR(50)`)
3. **Bitmaske** in `GameCell` (z.B. `TINYINT PencilBitmask` — Bits 0–8 für Zahlen 1–9)

**Entscheidung:**
**Variante 1 (eigene Tabelle).** Siehe **ER-Modell** und **Datenbank-Skript** Zeilen 180–192.

**Konsequenz:**
- (+) Konsistenz mit relationaler Cage-Modellierung (ADR-002)
- (+) CHECK-Constraint `CK_PencilMark_Value` (Range 1–9) auf DB-Ebene
- (+) Composite PK verhindert Duplikate (siehe **V13**)
- (+) Indexier-/Query-bar (z.B. "alle Marks für GameId X")
- (−) Mehr Rows in DB (worst case 81 × 9 = 729 Marks pro Game) — bei lokalem Setup unkritisch
- (−) JSON wäre kompakter, aber kein Schutz vor Invariant-Verletzungen

---

## ADR-009 — Pause via TotalPausedSeconds + PausedAt

**Status:** Akzeptiert

**Kontext:**
UC13 (Pause & Resume) erfordert, dass die gespielte Netto-Zeit korrekt berechnet wird, auch wenn der User mehrmals pausiert. Optionen:

1. **Multi-Row Pause-Log** — Separate Tabelle `PauseEvent (GameId, PausedAt, ResumedAt)`, Summe aller Differenzen ist die Pause-Zeit
2. **Single Row mit Accumulator** — Felder `IsPaused`, `PausedAt`, `TotalPausedSeconds` direkt in `Game`
3. **Nur StartTime adjustieren** — Bei Resume die `StartTime` nach hinten schieben

**Entscheidung:**
**Variante 2.** `Game` hat drei Pause-Felder:

- `IsPaused BIT` — aktuell pausiert ja/nein
- `PausedAt DATETIME2 NULL` — Zeitpunkt der aktuellen Pause
- `TotalPausedSeconds INT` — kumulierte Pause-Zeit über alle bisherigen Intervalle

Berechnung beim Game-Ende:
```
TimeSeconds = DATEDIFF(SECOND, StartTime, EndTime) - TotalPausedSeconds
```

Siehe **Datenbank-Skript** Zeilen 128–149 und **ER-Modell**.

**Konsequenz:**
- (+) Einfach zu queryen, kein Aggregat über Sub-Tabelle nötig
- (+) Audit-Spur durch `PausedAt` (letzter Pause-Beginn) — operative Sichtbarkeit
- (+) Konsistenz-Check `CK_Game_TotalPaused CHECK (TotalPausedSeconds >= 0)` (**V12**)
- (−) Keine Historie aller Pause-Events (z.B. "wie oft wurde pausiert"). Aus README nicht gefordert.
- (−) Resume-Logik muss atomic sein (read `PausedAt`, compute Δ, add to `TotalPausedSeconds`, set `IsPaused=0`, `PausedAt=NULL`) — wird via Service-Transaction abgesichert

---

## ADR-010 — Solver-Output: Limit auf 2 Lösungen

**Status:** Akzeptiert

**Kontext:**
Der Solver muss drei Fälle unterscheiden (siehe **V07**):

- 0 Lösungen → "Puzzle ist nicht lösbar"
- 1 Lösung → speichern (UC05) bzw. Hint geben (UC07)
- ≥ 2 Lösungen → "Lösung muss eindeutig sein"

Wenn der Solver mehrere Lösungen finden würde, könnte er stundenlang laufen (10⁹+ Möglichkeiten bei wenig vorgegebenen Zellen).

**Entscheidung:**
Der Solver-Algorithmus bricht **nach der zweiten** gefundenen Lösung ab. Rückgabe:

```csharp
public record SolveResult(int Solutions, int[,]? Solution);
// Solutions ∈ {0, 1, 2}
// 2 bedeutet: "mindestens 2 gefunden, weitere möglich"
```

Siehe UC11 Main Flow Schritt 5:
> "Bei Solutions >= 2 → nicht eindeutig (Solver bricht nach 2. gefundener Lösung ab)"

**Konsequenz:**
- (+) Performance: Worst-Case bounded, keine Abbruch-Heuristik nötig
- (+) Genügt für UC05-Entscheidung (Save ja/nein) und UC11-Aussage (Eindeutigkeit ja/nein)
- (+) Boundary-Test: UC11 AC11.2 "< 2 Sekunden"
- (−) Keine genaue Anzahl Lösungen bei ambiguous Puzzles — aber: nicht gefordert
- (−) Hint-Algorithmus muss bei `Solutions != 1` graceful failen (Hint nur bei eindeutiger Lösung sinnvoll)

---

## ADR-011 — Test-Pyramide: xUnit + bUnit + WebApplicationFactory + Playwright .NET

**Status:** Akzeptiert

**Kontext:**
README §3.2 fordert wörtlich:
> "Running the test cases should be implemented with a **test framework** and is part of the submission (e.g. JUnit in Java)."

Plus §3.1 (mind. 1 positiver, 1 negativer, ggf. 1 Boundary-Test pro UC).

Test-Pyramide-Optionen für den .NET-Stack:

| Layer | Framework-Optionen |
|-------|--------------------|
| Unit | xUnit, NUnit, MSTest |
| Component (Razor) | bUnit, AngleSharp-DOM-Setup |
| Integration | xUnit + `WebApplicationFactory<T>` (offiziell) |
| E2E | Playwright .NET, Selenium, Cypress |

**Entscheidung:**
- **Unit:** xUnit (Solver, Service-Methoden, Validation-Regeln)
- **Component:** bUnit (Razor-Components — Render, Event, State)
- **Integration:** xUnit + `WebApplicationFactory` (echte DB-Roundtrips, Auth-Flow)
- **E2E:** Playwright .NET (kritische User-Flows: Register → Login → Solve)

**Konsequenz:**
- (+) Alle Layer als `dotnet test` aufrufbar — eine CI-Pipeline
- (+) bUnit ist der De-facto-Standard für Blazor-Components (Microsoft-empfohlen)
- (+) Playwright .NET liefert auch UI-Test-Recording (Trace-Files)
- (+) Solver-Tests als reine Unit-Tests möglich, da Solver Pure Function ([§8.5](#chapter-8))
- (−) Vier Frameworks im Stack — Lernkurve für Prüfer, der das Test-Protokoll liest. Mitigation: Test-Protokoll listet Framework pro Test-Case in der CSV (**Test-Protokoll**).
- (−) Playwright benötigt Browser-Binary-Download beim ersten Run

---

## ADR-012 — Score-Formel: max(0, 10000 − TimeSeconds − HintsUsed × 300)

**Status:** Akzeptiert

**Kontext:**
README §2.1 UC8 fordert: "Implement rules for a high score based on the time needed and the number of hints used." Die konkrete Formel ist frei wählbar.

Anforderungen an die Formel:

- Schnellere Spiele → höherer Score
- Mehr Hints → niedrigerer Score (lt. UC07: "should be considered for the high score")
- Keine negativen Scores
- Ganzzahlig

**Entscheidung:**

```
Score = max(0, 10000 − TimeSeconds − HintsUsed × 300)
```

Siehe UC08 Score-Formel:

> - Begründung: Zeit-Penalty linear (1 Pkt/Sek); Hints kosten 5 Min Äquivalent
> - Floor bei 0 → keine negativen Scores

**Konsequenz:**
- (+) Einfach nachvollziehbar — Prüfer kann manuell prüfen
- (+) 5-Min-Penalty pro Hint ist eine "schmerzhafte" aber nicht ruinöse Wertung
- (+) `max(0, ...)` verhindert negative Scores → DB-Constraint `CK_Game_Score CHECK (Score >= 0)` erfüllt (**V12**)
- (−) Theoretisch könnte ein User in 9 999 Sekunden ohne Hints noch 1 Pkt erreichen — aber: nicht negativ, akzeptabel
- Test-Anker: **AC08.1** (Score ganzzahlig nach Formel)

---

## ADR-013 — Hint-Strategie: Naked-Single → Cage-Forced → Solver-Fallback

**Status:** Akzeptiert

**Kontext:**
UC07 (Ask for a Hint) verlangt einen Hint, der dem Spieler hilft. README:

> "Develop a **suggestion** for the hint, document it, and implement your solution."

Es gibt mehrere Hint-Qualitäten:

1. **Naked Single** — Zelle, in der nur 1 Wert constraint-konform möglich ist ("Logikhint")
2. **Cage-Forced** — Cage, dessen Belegung eindeutig durch die Summe + Constraints forciert ist
3. **Solver-Fallback** — wenn kein Logik-Hint möglich: einfach eine korrekte leere Zelle füllen

**Entscheidung:**
Hint-Algorithmus versucht in dieser Reihenfolge:

1. **Strategy A — Naked Single:** Suche eine leere Zelle, deren mögliche Werte nach Anwendung aller Constraints (Row/Col/Nonet/Cage) genau eine Zahl ergibt. Wenn gefunden: Hint enthält "Hier kann nur N stehen".
2. **Strategy B — Cage-Forced:** Wenn keine Naked-Single existiert, suche einen Cage, dessen Lösung eindeutig ist (z.B. 2-Zellen-Cage mit Sum=17 in unterschiedlichen Rows → muss `{8,9}` sein).
3. **Fallback:** Wenn beide Strategien scheitern (selten bei harten Puzzles), lass den Solver eine beliebige leere Zelle der bekannten Lösung füllen.

Siehe UC07 Hint-Vorschlag (dokumentiert).

**Konsequenz:**
- (+) Pädagogischer Mehrwert: Hint erklärt die Logik (Strategy A/B), nicht nur das Ergebnis
- (+) Solver wird nur als Fallback gebraucht → Performance besser bei einfachen Puzzles
- (+) HintsUsed-Counter wird in jedem Fall inkrementiert (siehe UC07 Main)
- (−) Hint-Algorithmus ist komplexer als ein reiner Solver-Aufruf — mehr Code, mehr Tests
- (−) Tests müssen alle drei Pfade abdecken (Strategy A, B, Fallback)
- Test-Anker: **AC07.1** (Hint füllt nur korrekte Zelle)

---

## ADR-014 — Auswahl der 3 zusätzlichen Use Cases

**Status:** Akzeptiert

**Kontext:**
README §2.3 fordert:
> "Add additional use cases to the use case diagram. Think of **3 additional** use cases that you can implement. Choose them based on the existing entries. Write down your decision in the documentation."

Kandidaten für zusätzliche UCs:

- Browse / Filter Puzzles
- Pause / Resume Game
- Pencil Marks (Candidate Notes)
- Edit Profile / Change Password
- Delete eigenes Puzzle
- Statistik-Dashboard
- Multiplayer / Live-Wettkampf
- Daily-Challenge

**Entscheidung:**
Gewählte UCs (siehe **Use-Cases-Dokument**):

- **UC12 — Browse / Filter Puzzles** — Praktischer UX-Mehrwert, DB-Query-relevant (Integration-Test-Anker)
- **UC13 — Pause / Resume Game** — Reale User-Anforderung (Sudoku-Spiele dauern lange), persistiert Game-State (Component- und Integration-Test-Anker)
- **UC14 — Pencil Marks** — Standard-Feature aller Sudoku-Apps, demonstriert UI-State-Management (Component-Test-Anker)

**Begründung der Auswahl (wörtlich aus **Use-Cases-Dokument**):**

> - UC12 Browse/Filter — Praktischer Mehrwert (UX), DB-Query-relevant (Integration-Tests)
> - UC13 Pause/Resume — Reale User-Anforderung (Sudoku-Spiele dauern lange), persistiert Game-State (Component + Integration)
> - UC14 Pencil Marks — Standard-Feature aller Sudoku-Apps, demonstriert UI-State-Management (Component-Test)
>
> → Die 3 UCs decken bewusst 3 verschiedene Test-Schwerpunkte ab.

**Konsequenz:**
- (+) Jeder UC zielt auf eine andere Test-Schicht (DB-Integration / Game-State / UI-Component) — gute Demonstration der Test-Pyramide aus [ADR-011](#adr-011--test-pyramide-xunit--bunit--webapplicationfactory--playwright-net)
- (+) Jeder UC ergänzt Kernfunktionen (Solve-Flow), ohne neue Personas einzuführen → Scope bleibt eng
- (+) Pencil Marks und Pause sind Standard-Feature-Erwartungen → User-Experience-Boost
- (−) Bewusst NICHT gewählt: Multiplayer (würde Auth- und Sync-Komplexität explodieren), Admin (zweite Persona, mehr Routen)
- (−) Browse-Filter erfordert Pagination-Logik (siehe UC12 AC12.2)

---

## ADR-015 — Single-Image Container für Prüferbequemlichkeit

**Status:** Akzeptiert

**Kontext:**
Die README-Aufgabenstellung verlangt explizit: "create a database named 'sudoku' on MySQL or MS-SQL server **running on your machine**" (§1.3). Das bedeutet, jeder Prüfer muss lokal eine MS-SQL-Instanz installieren, das Schema deployen, die App konfigurieren und starten. Diese Reibung ist für die Bewertung nicht förderlich.

Alternativen:

1. **Native-Only (Standard).** Prüfer installiert MS-SQL Express + .NET 10 SDK manuell, führt **Datenbank-Skript** aus, startet App via `dotnet run`. Standard und gut dokumentiert (siehe [§7.2 / §7.3](#chapter-7)) — aber: Setup-Reibung, Plattform-Drift macOS/Windows/Linux.
2. **Klassisches Docker-Compose mit 2 Services.** Ein `mssql`-Service + ein `app`-Service mit Network-Bridge. Best-Practice für Production (saubere Trennung, Skalierbarkeit) — aber: zwei Container, zwei Lifecycle-Probleme, Wait-for-DB-Race-Condition vor App-Start.
3. **Single-Image (DB + App in einem Container).** Multi-Stage Build mit `mssql/server:2022-latest` als Base + .NET-Runtime als Layer + Supervisor-Script (`docker-entrypoint.sh`) für sqlservr + dotnet. Anti-Pattern für Production, aber **ein einziger `docker run`-Befehl** für den Prüfer.

**Entscheidung:**
**Option 3 (Single-Image)** wird als primärer Container-Pfad ausgeliefert, parallel zur nativen Variante.

**Begründung:**
- Der Wettbewerbs-Kontext ist Submission, nicht Production. Der Prüfer bewertet ein Mal — Skalierung oder Update-Pfade sind nicht relevant.
- README §1.3 ("running on your machine") wird trotz Container erfüllt — der Container läuft auf der Maschine des Prüfers, die DB läuft im Container des Prüfers. Kein Cloud-Service involviert.
- Das Image wird bei jedem `main`-Push via **.github/workflows/deploy.yml** gebaut und nach `ghcr.io/tim-fischer-zh/killer-sudoku:latest` gepusht → Prüfer kann mit einem `docker pull` ohne Local-Build verifizieren.
- Supervisor-Script handhabt Race-Condition (Wait-for-DB), idempotenter Schema-Apply via Marker-File.
- Die Native-Variante bleibt als Fallback dokumentiert für Prüfer ohne Docker.

**Konsequenz:**
- (+) `docker run -p 8080:8080 ghcr.io/tim-fischer-zh/killer-sudoku:latest` und Browser → fertig.
- (+) Reproduzierbarkeit garantiert (Image-SHA in GHA-Tags).
- (+) MS-SQL EULA wird im Image akzeptiert (via `ACCEPT_EULA=Y`) — kein manuelles Setup.
- (−) Anti-Pattern für Production: zwei Prozesse in einem Container (Single-Process-Per-Container ist Best Practice).
- (−) Bigger image footprint (~ 1.5 GB inkl. mssql-Layer).
- (−) Logs der zwei Prozesse vermischen sich (stdout). Akzeptabel für Submission-Kontext.
- (−) Healthcheck deckt nur die App ab (`curl :8080/health`), nicht die DB direkt — DB-Health wird implizit über App-Health geprüft.

**Operatives Setup:**
- `Dockerfile`, `docker-entrypoint.sh`, `docker-compose.yml`, `docker-compose.prod.yml`, `.dockerignore` im Repo-Root
- GitHub-Actions-Workflow für Build & Push nach `ghcr.io` sowie Production-Deploy (siehe ADR-016)
- Persistenz via Named-Volume `mssql-data`
- Connection-String über `ConnectionStrings__Sudoku` Env-Var override-bar

→ Vollständiges Setup in [§7.5 Container-Deployment](#chapter-7).

---

## ADR-016 — Production-Deployment via Self-Hosted Runner + Cloudflare-Tunnel

**Status:** Akzeptiert

**Kontext:**
Über das von der Aufgabenstellung geforderte lokale Setup hinaus soll die App **zusätzlich** als Live-Demo unter einer öffentlichen Domain (`web17skill.com`) erreichbar sein, damit der Prüfer eine bereits laufende Instanz aufrufen kann, ohne lokal etwas installieren zu müssen. Diese Production-Variante ist **nicht** Pflicht laut Spec — sie ist ein Bonus.

Zu entscheiden waren zwei Achsen:

1. **Deploy-Trigger:** SSH-Deploy aus GitHub-Hosted-Runner vs. Self-Hosted-Runner direkt auf der VPS.
2. **Public-Exposure:** Klassischer Reverse-Proxy mit offenem Port 443 (Nginx/Caddy + Let's Encrypt) vs. Cloudflare-Tunnel (outbound initiiert, keine offenen Ports).

Alternativen:

| Variante | Deploy | Exposure | Bewertung |
|----------|--------|----------|-----------|
| A | SSH-Action aus GitHub-Hosted | Nginx + Let's Encrypt | klassisch, aber: SSH-Key-Verwaltung, offene Firewall-Regeln (80/443), eigene TLS-Renewal-Logik |
| B | SSH-Action aus GitHub-Hosted | Cloudflare-Tunnel | weniger Firewall-Aufwand, aber: SSH-Key bleibt Angriffsfläche |
| C | Self-Hosted-Runner auf VPS | Cloudflare-Tunnel | kein SSH-Pfad, kein offener Port, lokale Docker-Builds nutzen lokalen Cache |

**Entscheidung:**
**Option C** — Self-Hosted-Runner direkt auf der VPS in Kombination mit Cloudflare-Tunnel.

**Begründung:**
- Der Runner ist gleichzeitig Docker-Host → `docker build` + `docker compose up` laufen ohne Image-Transport (lokaler Daemon-Cache wird zwischen Runs wiederverwendet).
- Kein SSH-Mechanismus nötig → kein Deploy-Key, keine `appleboy/ssh-action`-Dependency.
- Cloudflare-Tunnel ist outbound initiiert → keine eingehenden Ports auf der VPS offen; TLS terminiert bei Cloudflare (managed Cert, automatische Erneuerung).
- DDoS-Mitigation und WAF inkludiert.
- Der GHCR-Push (Tags `latest` + `<short-sha>`) bleibt erhalten — die Production-Compose nutzt zwar das lokal gebaute Image, aber die Tags in der Registry erlauben jederzeit Rollback oder externes Pull-Deployment.

**Konsequenz:**
- (+) Push auf `main` → automatischer Live-Deploy in unter 2 Minuten inkl. Smoke-Test gegen `https://web17skill.com/health`.
- (+) Keine externe Cloud-Plattform — VPS bleibt unter eigener Kontrolle, Cloudflare ist nur als Tunnel-Endpoint involviert (keine Anwendungslogik bei Cloudflare).
- (+) Container bindet bewusst nur auf Loopback (`127.0.0.1:80:8080`) — selbst bei Cloudflare-Tunnel-Ausfall ist die App nicht von außen direkt erreichbar.
- (−) Der Self-Hosted-Runner ist ein zusätzlicher Service auf der VPS, der gewartet werden muss (Updates, Logs).
- (−) Wenn die VPS offline ist, kann nicht deployed werden (kein Fallback auf GitHub-Hosted-Runner). Für den Submission-Kontext akzeptabel.

**Operatives Setup:**
- `.github/workflows/deploy.yml` mit `runs-on: self-hosted`
- `docker-compose.prod.yml` mit Loopback-Bind und ohne SQL-Port-Publish
- `cloudflared` als systemd-Service auf der VPS
- GitHub-Secrets: `GHCR_TOKEN` (Classic-PAT), `MSSQL_SA_PASSWORD`

→ Vollständiges Setup in [§7.6 Production-Deployment](#chapter-7).

---

## Übersicht aller ADRs

| ADR | Titel | Bezug |
|-----|-------|-------|
| [ADR-001](#adr-001--stack-net-10--blazor-server--ms-sql-server) | Stack: .NET 10 + Blazor Server + MS-SQL | [§7](#chapter-7), [§8](#chapter-8) |
| [ADR-002](#adr-002--cage-modellierung-relational-cage--cagecell) | Cage relational modelliert | **ER-Modell**, **Datenbank-Skript** |
| [ADR-003](#adr-003--highscore-als-view-statt-tabelle) | Highscore als View | UC08, `vw_Highscore` |
| [ADR-004](#adr-004--eine-zelle-pro-cage-pro-puzzle-via-trigger) | Cell-Uniqueness via Trigger | UC04, V06 |
| [ADR-005](#adr-005--appuser-statt-user-t-sql-reserved-keyword) | AppUser statt User | T-SQL Reserved |
| [ADR-006](#adr-006--rowidx--colidx-statt-row--col) | RowIdx/ColIdx statt Row/Col | T-SQL Reserved |
| [ADR-007](#adr-007--keine-solution-spalte-in-puzzle) | Keine Solution-Spalte | UC04 strict |
| [ADR-008](#adr-008--pencil-marks-als-eigene-tabelle-statt-json-in-gamecell) | Pencil Marks als eigene Tabelle | UC14 |
| [ADR-009](#adr-009--pause-via-totalpausedseconds--pausedat) | Pause via TotalPausedSeconds | UC13 |
| [ADR-010](#adr-010--solver-output-limit-auf-2-lösungen) | Solver limit 2 Lösungen | UC11, V07 |
| [ADR-011](#adr-011--test-pyramide-xunit--bunit--webapplicationfactory--playwright-net) | Test-Pyramide | README §3.2 |
| [ADR-012](#adr-012--score-formel-max0-10000--timeseconds--hintsused--300) | Score-Formel | UC08 |
| [ADR-013](#adr-013--hint-strategie-naked-single--cage-forced--solver-fallback) | Hint-Strategie | UC07 |
| [ADR-014](#adr-014--auswahl-der-3-zusätzlichen-use-cases) | 3 zusätzliche UCs | README §2.3 |
| [ADR-015](#adr-015--single-image-container-für-prüferbequemlichkeit) | Single-Image Docker (DB + App) | [§7.5](#chapter-7) |
| [ADR-016](#adr-016--production-deployment-via-self-hosted-runner--cloudflare-tunnel) | Production-Deploy via Self-Hosted Runner + Cloudflare-Tunnel | [§7.6](#chapter-7) |

---

## Verweise

- [Kapitel 1 — Einführung und Ziele](#chapter-1)
- [Kapitel 7 — Verteilungssicht](#chapter-7)
- [Kapitel 8 — Querschnittliche Konzepte](#chapter-8)
- **Use-Cases-Dokument**
- **Validation-Regeln**
- **ER-Modell**
- **Funktionalitäts-Matrix**
- **Datenbank-Skript**
- **Aufgabenstellung** — Aufgabenstellung (autoritativ)
