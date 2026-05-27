# 12 Glossar

> arc42 v8.2 · Skills Battle 2026 — Application Development — Killer Sudoku
> Quelldokument (autoritativ): [`skillsbattle2026_1.1.md`](../../skillsbattle2026_1.1.md)

Alle fachlichen und technischen Begriffe der Architektur-Doku, alphabetisch sortiert. Quellenangaben verweisen auf das Wettbewerbs-README (`skillsbattle2026_1.1.md`) bzw. die ergänzenden Quell-Dokumente unter [`docs/`](..).

---

| Term | Definition · Quelle |
|------|---------------------|
| **AC (Acceptance Criterion)** | Testbares Kriterium pro Use Case; mappt 1:1 auf einen Test-Case im Test-Protokoll. Siehe Notation in [use-cases.md](../use-cases.md). |
| **AppUser** | Benutzer-Entität in der DB. Namens-Workaround, weil `User` ein reserviertes T-SQL-Keyword ist (siehe [R08](./11-risks.md#r08--reserved-keyword-konflikte-in-t-sql-user-row-col)). Tabellen-Definition: [db/sudoku.sql](../../db/sudoku.sql) Zeile 36. |
| **arc42** | Architektur-Dokumentations-Template (arc42.org), v8.2, 12 Kapitel. Siehe Index [00-README.md](./00-README.md). |
| **Auto-Solver** | Algorithmus aus UC11, der ein Puzzle automatisch löst. Service-Interface `ISolverService`. Siehe [use-cases.md UC11](../use-cases.md#uc11--auto-solve). |
| **Backtracking** | Solver-Strategie: rekursive Tiefensuche mit Constraint-Checks, Rücksprung bei Konflikt. Eingesetzt im `SolverService` für Killer-Sudoku (UC11). |
| **bUnit** | .NET Testing Library für Blazor-Component-Tests. Verwendet für Component-Level-Tests von `PuzzleGrid.razor` etc. (siehe [functionality.md](../functionality.md)). |
| **Cage** | Zell-Gruppe mit vorgegebener Summe (Killer-Sudoku-spezifisch). README §1: "The sum of all numbers in a cage must match the small number printed in its corner." |
| **Cage-Sum** | Soll-Summe eines Cages, Wertebereich 1–45 (1 Zelle min 1, 9 Zellen max 1+…+9 = 45). DB-Constraint `CK_Cage_Sum_Range` in [db/sudoku.sql](../../db/sudoku.sql). |
| **Cell / Zelle** | Ein Feld im 9×9-Grid; identifiziert durch `(RowIdx, ColIdx)` mit Werten 0–8. Wert einer Spielzelle: 1–9 oder leer. |
| **Difficulty** | Schwierigkeitsgrad 1, 2 oder 3. README §2.1 UC4: "the difficulty level from 1-3". DB-Constraint `CK_Puzzle_Difficulty`. |
| **Game** | Eine Spielsession eines Users an einem Puzzle. Tabellen-Definition: [db/sudoku.sql](../../db/sudoku.sql) Zeile 128. |
| **Hint** | Algorithmischer Tipp; UC07. Strategie: Naked-Single → Cage-Forced → Solver-Fallback (siehe [use-cases.md UC07](../use-cases.md#uc07--ask-for-a-hint)). |
| **Highscore** | Score-Ranking gemäss Formel UC08; persistiert als View `vw_Highscore` (siehe [db/sudoku.sql](../../db/sudoku.sql) Zeile 215). |
| **HintsUsed** | Anzahl der in einem Game verwendeten Hints; score-relevant gemäss UC08-Formel. README §2.1 UC8: "based on the time needed and the number of hints used". |
| **Killer Sudoku** | Sudoku-Variante mit zusätzlichen Cage-Summen. README §1: "various tricky variants. One variant is to fill in the squares based on a set of sums over several squares." |
| **Nonet** | Ein 3×3-Block im Sudoku-Grid; insgesamt 9 Nonets. README §1: "Each row, column, and **nonet** contains each number exactly once". |
| **Pencil Mark** | Kandidaten-Notiz pro Zelle (kleine Markierungen 1–9 als mögliche Werte). UC14, eigene Tabelle `PencilMark`. |
| **Puzzle** | Eine Killer-Sudoku-Aufgabe (Cages + Difficulty). README §2.1 UC4: "No solution is recorded" — Lösung wird **nicht** persistiert. |
| **Score** | Punkte gemäss Formel `max(0, 10000 − TimeSeconds − HintsUsed × 300)` (siehe [use-cases.md UC08](../use-cases.md#uc08--show-high-score)). |
| **SignalR** | Real-Time-Kommunikations-Framework für Blazor-Server-Circuits. Permanente Verbindung Server ↔ Client (siehe [functionality.md "Stack-Konventionen"](../functionality.md#stack-konventionen)). |
| **Solvability** | Existenz mindestens einer Lösung. UC05-Bedingung: "The puzzle can only be saved if it is solvable." (README §2.1 UC5). |
| **Solver** | Domänen-Service `ISolverService` (UC11). Interface-Definition: [functionality.md "Service-Interfaces"](../functionality.md#service-interfaces-komplett-für-unit-tests). |
| **Sum-Check 405** | Schnell-Validierung: Summe aller 81 Zellen-Werte = 9 × 45 = **405**. README §2.3: "Calculate the sum of all numbers in the completed Sudoku. The value can be determined unambiguously." Eingesetzt **vor** der vollständigen Algorithmus-Validierung. Siehe [V09](../validation.md#v09--sum-check-lösung-uc09). |
| **TDD** | Test-Driven Development; Workflow Red → Green → Refactor. README §3.1: "Plan your test cases **before** you start the implementation." |
| **TimeSeconds** | Spieldauer in Sekunden, abzüglich Pausen: `TimeSeconds = DATEDIFF(SECOND, StartTime, EndTime) − TotalPausedSeconds`. Score-relevant (UC08). Siehe [UC10](../use-cases.md#uc10--save-result) und [UC13](../use-cases.md#uc13--pause--resume-game). |
| **Uniqueness** | Lösung muss eindeutig sein. README §1: "The solution **must be unique**." Strict-Wort; Multi-Solution-Puzzles werden abgelehnt (siehe [R02](./11-risks.md#r02--multi-solution-erkennung-übersehen-pattern-2-spec-detail-blindheit), [V07](../validation.md#v07--puzzle-solvability-uc05)). |
| **Use Case** | Anwendungsfall, Notation siehe [use-cases.md](../use-cases.md). UC01–UC11 aus README §2.1, UC12–UC14 selbst gewählt gemäss README §2.3. |
| **V01–V16** | Validation-Regeln aus [validation.md](../validation.md). Jede Regel ist Layer-zugeordnet (Client / Server / DB). |
| **xUnit** | .NET Unit-Testing-Framework, Standard-Wahl für Service-/Solver-Tests (siehe [functionality.md "Kritische Abhängigkeiten"](../functionality.md#kritische-abhängigkeiten-für-test-order)). |
