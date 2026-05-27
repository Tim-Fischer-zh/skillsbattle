<h1 id="chapter-11">11 Risiken und technische Schulden</h1>

> arc42 v8.2 · Skills Battle 2026 — Application Development — Killer Sudoku
> Quelldokument (autoritativ): **Aufgabenstellung**

Dieses Kapitel listet Wettkampf-spezifische Risiken (R01–R08) sowie bewusste technische Schulden (TD01–TD03) zum Stand der Architektur-Festschreibung. Bewertungs-Skalen:

- **Wahrscheinlichkeit (W):** niedrig / mittel / hoch
- **Auswirkung (A):** niedrig / mittel / hoch / kritisch (kritisch = führt direkt zu UC-/AC-Verletzung)

---

## §11.1 Wettkampf-Risiken

### R01 — Solver-Performance bei worst-case-Cage-Konfigurationen

| Attribut | Wert |
|----------|------|
| Beschreibung | Backtracking-Solver kann bei dünn besetzten oder ungünstig zerlegten Cage-Layouts in eine kombinatorische Explosion laufen. |
| Wahrscheinlichkeit | mittel |
| Auswirkung | hoch — Verletzung von Quality-Szenario [Q02](#chapter-10) (≤ 2 s) |
| Mitigation | Constraint-Propagation pro Cage (Min-/Max-Sum-Bounds), Variable-Ordering MRV (Most-Restricted-Variable), harter Timeout im Service. |
| Test-Anker | UC11 → AC11.2 (siehe use-cases.md UC11) |

### R02 — Multi-Solution-Erkennung übersehen (Pattern 2 "Spec-Detail-Blindheit")

| Attribut | Wert |
|----------|------|
| Beschreibung | Naïve Solver-Implementierung gibt die erste gefundene Lösung zurück und stoppt — verletzt das README-Strict-Wort "The solution **must be unique**." (§1). Multi-Solution-Puzzles würden fälschlich gespeichert. |
| Wahrscheinlichkeit | hoch (klassischer Fehler bei Killer-Sudoku-Implementierungen) |
| Auswirkung | kritisch — Verletzung von **V07** und Top-Ziel **Q2** |
| Mitigation | Solver sucht aktiv nach **2.** Lösung und bricht erst dann ab; `SolveResult.Solutions ∈ {0, 1, 2}`; UC05 speichert ausschliesslich bei `Solutions == 1`. |
| Test-Anker | UC05 → AC05.2; UC11 → AC11.1 |

### R03 — SQL-Constraint-Lücken (Cage-Trigger fehleranfällig)

| Attribut | Wert |
|----------|------|
| Beschreibung | Der INSTEAD-/AFTER-Trigger `trg_CageCell_UniquePerPuzzle` (siehe **Datenbank-Skript** ab Zeile 99) ist die einzige DB-seitige Garantie, dass eine Zelle nicht in zwei Cages desselben Puzzles liegt. Fehler im Trigger oder Batch-Inserts mit deaktiviertem Trigger könnten inkonsistente Daten zulassen. |
| Wahrscheinlichkeit | niedrig |
| Auswirkung | hoch — Datenintegritäts-Verletzung gegenüber **V06** |
| Mitigation | Defense in Depth — Application-Layer-Validierung in `IPuzzleService.ValidateStructureAsync` zusätzlich zur Trigger-Prüfung; Integration-Test mit absichtlich kollidierendem Insert. |
| Test-Anker | UC04 → AC04.2; Integration-Test "Doppelte Cage-Cell wird vom Trigger geblockt" |

### R04 — Blazor-Server-Circuit-Disconnects bei langen Pausen (UC13)

| Attribut | Wert |
|----------|------|
| Beschreibung | Blazor-Server-Apps halten pro User eine SignalR-Circuit (siehe **Funktionalitäts-Matrix** "SignalR-Verbindung permanent"). Bei Pause + Tab-Wechsel kann die Circuit getrennt werden — laufende Game-State-Manipulationen würden verloren gehen, falls UC13-Auto-Save nicht greift. |
| Wahrscheinlichkeit | mittel |
| Auswirkung | mittel — UC13 AC13.2 (Resume restored alle Zellen) bedroht |
| Mitigation | Persistenz pro Move (UC13 "Browser-Tab schliessen ohne Pause → State wird trotzdem persistiert (auto-save bei jedem Move)"), serverseitiges Resume aus DB statt aus Circuit-State. |
| Test-Anker | UC13 → AC13.1, AC13.2 |

### R05 — Test-Coverage-Schwächen bei Solver-Edge-Cases

| Attribut | Wert |
|----------|------|
| Beschreibung | Solver hat viele Code-Pfade (Cage mit 1 Zelle, Cage über ganze Zeile, unlösbares Puzzle, mehrdeutiges Puzzle, leeres Grid). Ohne gezielten Edge-Case-Plan kann die 80%-Marke (Quality-Szenario [Q06](#chapter-10)) am Solver allein scheitern. |
| Wahrscheinlichkeit | mittel |
| Auswirkung | hoch — verletzt Top-Ziel **Q3** und damit Submissions-Qualität |
| Mitigation | Solver-Unit-Tests systematisch nach der Edge-Case-Liste in functionality.md "Kritische Abhängigkeiten" (5+ Tests: README-Beispiele, unlösbar, mehrdeutig, 1-Zell-Cage, voller Zeilen-Cage). |
| Test-Anker | UC11 — separate Test-Klasse `SolverServiceTests` |

### R06 — Sum-Check Pre-Validation übersehen (Pattern 5 "Improvisierter Format-String")

| Attribut | Wert |
|----------|------|
| Beschreibung | README §2.3 verlangt explizit: "Use the value [Total = 405] for a 'simple' validation **before** checking the solution with an algorithm." Eine Implementierung, die direkt den vollen Algorithmus aufruft, ignoriert die README-Vorgabe — auch wenn das Endergebnis korrekt ist. |
| Wahrscheinlichkeit | mittel |
| Auswirkung | mittel — Verletzung einer explizit benannten Spec-Anforderung |
| Mitigation | `IGameService.CheckSolutionAsync` ruft als ersten Schritt den Sum-Check (siehe **V09**) auf; Unit-Test verifiziert, dass bei `Summe ≠ 405` der Solver nicht aufgerufen wird. |
| Test-Anker | UC09 → AC09.1 |

### R07 — Spec-Verletzung "No solution is recorded" durch versehentliches Speichern

| Attribut | Wert |
|----------|------|
| Beschreibung | README §2.1 UC4: "No solution is recorded." Wenn z.B. zu Debug-Zwecken oder per ORM-Auto-Migration eine Spalte `Solution` in `Puzzle` landet, wird die Spec verletzt — auch wenn die Spalte leer bleibt. |
| Wahrscheinlichkeit | niedrig |
| Auswirkung | hoch — direkte Verletzung eines Strict-Wortes der Spec |
| Mitigation | Schema in **Datenbank-Skript** ohne Solution-Spalte; Integration-Test gegen `INFORMATION_SCHEMA.COLUMNS` stellt sicher, dass keine `Solution`-Spalte existiert (siehe **ER-Modell**). |
| Test-Anker | UC04 → AC04.4 |

### R08 — Reserved-Keyword-Konflikte in T-SQL (User, Row, Col)

| Attribut | Wert |
|----------|------|
| Beschreibung | Die in der Domäne natürlichen Namen `User`, `Row`, `Col` sind in T-SQL reservierte Keywords. Ohne Anpassung schlagen DDL-/DML-Statements fehl oder benötigen Bracket-Quoting `[User]`. |
| Wahrscheinlichkeit | hoch (würde ohne Umbenennung sicher auftreten) |
| Auswirkung | mittel (Build-Fail, leicht zu beheben — aber unschön und Quelle für Folgebugs) |
| Mitigation | Umbenennung gemäss erm.md Design-Entscheidungen: Tabelle `AppUser` statt `User`, Spalten `RowIdx` / `ColIdx` statt `Row`/`Col`. Bereits konsistent in **Datenbank-Skript** umgesetzt. |
| Test-Anker | Build-/Migrations-Lauf des **Datenbank-Skript**-Scripts |

---

## §11.2 Technische Schulden (Stand 2026-05-27, vor App-Implementierung)

Die folgenden Schulden sind **bewusste Entscheidungen** für den Wettkampf-Scope. Jede Schuld nennt den Grund und die Konsequenz für einen späteren Produktiv-Einsatz.

### TD01 — Doppelte Schema-Wahrheit: EF-Migrations + generiertes sudoku.sql

| Attribut | Wert |
|----------|------|
| Beschreibung | EF-Migrations sind Source of Truth (`KillerSudoku.Data/Persistence/Migrations/`). Das von README §1.3 geforderte **Datenbank-Skript** wird per `dotnet ef migrations script --idempotent -o db/sudoku.sql` aus den Migrations exportiert. |
| Begründung | README §1.3 verlangt explizit ein **Datenbank-Skript**-Skript für den Prüfer; gleichzeitig braucht ein .NET-/EF-Workflow Migrations für reproduzierbare Schema-Evolution. Beides parallel zu pflegen ist günstiger als nur eine Variante. |
| Folge bei Produktiv-Use | Bei manuellen Änderungen am SQL-Skript ohne entsprechende Migration entsteht Drift — Disziplin nötig: Schema-Änderungen immer über Migrations, dann **Datenbank-Skript** neu exportieren. Im CI kann ein Drift-Check ergänzt werden (`dotnet ef migrations script` ↔ commited **Datenbank-Skript** vergleichen). |

### TD02 — Keine i18n-Vorbereitung (UI nur Deutsch)

| Attribut | Wert |
|----------|------|
| Beschreibung | UI-Texte (Fehlermeldungen, Labels, Validierungs-Strings) sind direkt in Razor-Components hardcoded in **Deutsch**. Kein `IStringLocalizer`, keine `.resx`-Files. |
| Begründung | Wettkampf-Scope; README definiert keine Mehrsprachigkeit; reduziert Aufwand zugunsten Test-Coverage. |
| Folge bei Produktiv-Use | Vollständiges Refactoring zu Localizer notwendig für Englisch/Französisch; Test-Updates für lokalisierte Strings. |

### TD03 — Keine Multi-User-Concurrency-Tests (Blazor-Server-Single-Circuit-Annahme)

| Attribut | Wert |
|----------|------|
| Beschreibung | Tests gehen davon aus, dass pro User maximal **eine** aktive Blazor-Circuit existiert. Race-Conditions bei parallelen Tabs / mehreren Devices desselben Users werden nicht durch Tests abgedeckt. |
| Begründung | Wettkampf-Scope. UC13 AC13.3 fordert lediglich "Nur 1 aktives Game pro User-Puzzle-Kombi gleichzeitig" — das ist über den DB-Filtered-Index `UX_Game_ActiveOnly` (siehe **Datenbank-Skript** Zeile 155) abgesichert, nicht über App-Tests. |
| Folge bei Produktiv-Use | Concurrency-/Last-Tests erforderlich; ggf. Optimistic-Concurrency-Token auf `Game` (rowversion). |

---

## §11.3 Querverweise

- [§10.2 Quality-Szenarien](#chapter-10) — die durch Risiken bedrohten Antwortmasse
- Validation V01–V16
- Use Cases mit Acceptance Criteria
- ER-Modell und Design-Entscheidungen
- DB-Schema
