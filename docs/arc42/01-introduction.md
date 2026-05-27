# 1 Einführung und Ziele

> arc42 v8.2 · Skills Battle 2026 — Application Development — Killer Sudoku
> Quelldokument (autoritativ): [`skillsbattle2026_1.1.md`](../../skillsbattle2026_1.1.md)

---

## §1.1 Aufgabenstellung

Die Anwendung implementiert eine **Killer-Sudoku-Variante** als Web-Applikation. Anstelle des klassischen Sudoku werden zusätzlich **Cage-Summen** (Summen über mehrere Zellen) als Constraints vorgegeben. Die App muss Plan, Implementation und Tests gemäss den im Wettbewerbs-README spezifizierten Use Cases liefern.

### Spielregeln (wörtlich aus README §1)

Die Lösung eines Killer-Sudoku-Puzzles muss vier Bedingungen erfüllen (README §1, "the following conditions are met"):

> "Each row, column, and nonet contains each number **exactly once**."
> "The sum of all numbers in a cage **must match** the small number printed in its corner."
> "**No** number appears more than once in a cage."
> "The solution **must be unique**."

Diese vier Regeln sind in der Anwendung als Validierungslogik abzubilden (siehe [V09](../validation.md#v09--sum-check-lösung-uc09), [V10](../validation.md#v10--lösungs-validierung-vollständig-uc09)) und werden in Use Case [UC09](../use-cases.md#uc09--check-solution) sowie im Solver [UC11](../use-cases.md#uc11--auto-solve) geprüft.

### Funktionaler Scope

Die Anwendung deckt die im README §2.1 vorgegebenen Use Cases **UC01–UC11** sowie drei selbst gewählte zusätzliche Use Cases **UC12–UC14** ab (gemäss README §2.3, "Think of 3 additional use cases"):

- **UC01–UC11** — Read Rules, Register, Login, Enter Puzzle, Save Puzzle (Solvability-Check), Solve Puzzle, Hint, Highscore, Check Solution, Save Result, Auto Solve
- **UC12** — Browse / Filter Puzzles
- **UC13** — Pause & Resume Game
- **UC14** — Pencil Marks

Eine vollständige Übersicht inkl. Acceptance Criteria findet sich in [`use-cases.md`](../use-cases.md). Die Service-/Screen-/DB-Zuordnung ist in [`functionality.md`](../functionality.md) tabelliert.

### Design-Freiheit (README §1.1)

Das README erlaubt explizite Designfreiheit: "You are free to design the application. Your task is to create mockups." Die Mockups sind in [`mockup-briefs.md`](../mockup-briefs.md) als Figma-Briefings dokumentiert (Screens S1–S7 + Layout).

---

## §1.2 Qualitätsziele

Die folgenden drei Top-Qualitätsziele leiten alle Architektur- und Implementierungsentscheidungen. Sie sind so gewählt, dass sie aus den **strikten Vorgaben des README** ableitbar und über Tests messbar sind.

| # | Qualitätsziel | Begründung (README-Bezug) | Messbarkeit |
|---|---------------|---------------------------|-------------|
| **Q1** | **Korrektheit des Solvers (Funktionale Korrektheit)** | README §1 fordert "The solution **must be unique**." und §2.1 UC4 fordert "Solutions **must be calculated with an algorithm**." Der Solver ist die zentrale Logik-Komponente — falsche Ergebnisse zerstören UC05, UC07, UC09. | Solver-Unit-Tests gegen die 2 README-Beispiele + Edge-Cases (unlösbar, mehrdeutig). Performance-Boundary < 2s (siehe [AC11.2](../use-cases.md#uc11--auto-solve)). |
| **Q2** | **Lösungs-Eindeutigkeit (Konsistenz beim Speichern)** | README §1 "The solution must be unique." + §2.1 UC5 "The puzzle **can only be saved if it is solvable**." Multi-Solution-Puzzles werden abgelehnt — nicht nur Zero-Solution (siehe [V07](../validation.md#v07--puzzle-solvability-uc05)). | Integration-Test: Speichern eines mehrdeutigen Puzzles → kein DB-Row. |
| **Q3** | **Test-Abdeckung & Test-Plan-Termin** | README §3.1 fordert pro UC mind. 1 positiver + 1 negativer + (wo möglich) 1 Boundary-Test. README §1.4: "submission of the planned test cases must take place **by 12 o'clock**." | Test-Plan vor 12:00 abgegeben; Unit-Tests in Test-Framework lt. README §3.2 ("**should be implemented with a test framework**"). Coverage-Ziel siehe [Kapitel 10](./10-quality.md). |

### Weitere relevante Qualitätsmerkmale (sekundär)

- **Sicherheit** — README §2.1 UC2: "a login **is required**." → Authentifizierung, geschützte Routen, sichere Passwort-Speicherung (siehe [V03](../validation.md#v03--passwort-uc02-uc03), [V16](../validation.md#v16--authorization-alle-geschützten-seitenendpoints)).
- **Bedienbarkeit** — README §1.1 fordert "appropriate navigation" + "controls for all functions" → vollständig adressiert in [`mockup-briefs.md`](../mockup-briefs.md).
- **Wartbarkeit** — Klare Trennung Service-Layer / DB-Layer / UI (siehe Service-Interfaces in [`functionality.md`](../functionality.md#service-interfaces-komplett-für-unit-tests)).

---

## §1.3 Stakeholder

| Rolle | Erwartung | Berührungspunkte |
|-------|-----------|------------------|
| **Wettbewerbs-Prüfer** (Skills Battle Jury) | Konforme Submission gemäss README §1.4 — ZIP-File, Doku als PDF, Source-Code, Executables, DB-Script. Bewertung gegen UCs und Test-Protokoll. | `AppDev_Name_FirstName.zip` (siehe [Kapitel 2 §2.2](./02-constraints.md#22-organisatorische-randbedingungen)). |
| **Wettbewerber** (Tim Fischer, Implementierer) | Zeit-effiziente, testbare Architektur. Strikte Vermeidung von Scope-Creep. Reproduzierbares lokales Setup. | Source-Code, Test-Suite, lokaler Build mit `dotnet`. |
| **End-Nutzer** (Spieler der App) | Funktionierender Killer-Sudoku-Spielfluss: Account → Puzzle wählen → spielen → Hint → Lösung prüfen → Highscore. UI-Sprache deutsch. | UI-Screens S1–S7 ([`mockup-briefs.md`](../mockup-briefs.md)). |
| **Puzzle-Ersteller** (User-Subgruppe, kreiert Puzzles via UC04/UC05) | Verlässliche Solvability-Prüfung, kein Datenverlust bei Reject. | Screen S5 — EnterPuzzle ([`mockup-briefs.md`](../mockup-briefs.md#screen-s5--enter-puzzle-editor)). |

### Nicht-Stakeholder (bewusst ausgegrenzt)

Folgende Rollen sind im Scope **nicht** vorgesehen (Begründung: nicht in README spezifiziert, keine UC-Anker, siehe Anti-Halluzinations-Notiz in [`mockup-briefs.md`](../mockup-briefs.md#anti-halluzinations-notiz-für-figma-claude)):

- Admin-Rolle / Moderation
- Externe Systeme (Social-Login, Payment, Analytics)
- Mehrspieler-/Live-Funktionen

---

## Verweise

- [Use Cases UC01–UC14](../use-cases.md)
- [Validation Rules V01–V16](../validation.md)
- [Funktionalitäts-Matrix](../functionality.md)
- [Kapitel 2 — Randbedingungen](./02-constraints.md)
- [Kapitel 3 — Kontextabgrenzung](./03-context.md)
