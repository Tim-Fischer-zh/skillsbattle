<h1 id="chapter-1">1 Einführung und Ziele</h1>

> arc42 v8.2 · Skills Battle 2026 — Application Development — Killer Sudoku
> Quelldokument (autoritativ): **Aufgabenstellung**

---

## §1.1 Aufgabenstellung

Die Anwendung implementiert eine **Killer-Sudoku-Variante** als Web-Applikation. Anstelle des klassischen Sudoku werden zusätzlich **Cage-Summen** (Summen über mehrere Zellen) als Constraints vorgegeben. Die App muss Plan, Implementation und Tests gemäss den im Wettbewerbs-README spezifizierten Use Cases liefern.

### Spielregeln (wörtlich aus README §1)

Die Lösung eines Killer-Sudoku-Puzzles muss vier Bedingungen erfüllen (README §1, "the following conditions are met"):

> "Each row, column, and nonet contains each number **exactly once**."
> "The sum of all numbers in a cage **must match** the small number printed in its corner."
> "**No** number appears more than once in a cage."
> "The solution **must be unique**."

Diese vier Regeln sind in der Anwendung als Validierungslogik abzubilden (siehe **V09**, **V10**) und werden in Use Case **UC09** sowie im Solver **UC11** geprüft.

### Funktionaler Scope

Die Anwendung deckt die im README §2.1 vorgegebenen Use Cases **UC01–UC11** sowie drei selbst gewählte zusätzliche Use Cases **UC12–UC14** ab (gemäss README §2.3, "Think of 3 additional use cases"):

- **UC01–UC11** — Read Rules, Register, Login, Enter Puzzle, Save Puzzle (Solvability-Check), Solve Puzzle, Hint, Highscore, Check Solution, Save Result, Auto Solve
- **UC12** — Browse / Filter Puzzles
- **UC13** — Pause & Resume Game
- **UC14** — Pencil Marks

Eine vollständige Übersicht inkl. Acceptance Criteria findet sich in **Use-Cases-Dokument**. Die Service-/Screen-/DB-Zuordnung ist in **Funktionalitäts-Matrix** tabelliert.

### Design-Freiheit (README §1.1)

Das README erlaubt explizite Designfreiheit: "You are free to design the application. Your task is to create mockups." Die Mockups sind in **Mockup-Briefings** als Figma-Briefings dokumentiert (Screens S1–S7 + Layout).

---

## §1.2 Qualitätsziele

Die folgenden drei Top-Qualitätsziele leiten alle Architektur- und Implementierungsentscheidungen. Sie sind so gewählt, dass sie aus den **strikten Vorgaben des README** ableitbar und über Tests messbar sind.

| # | Qualitätsziel | Begründung (README-Bezug) | Messbarkeit |
|---|---------------|---------------------------|-------------|
| **Q1** | **Korrektheit des Solvers (Funktionale Korrektheit)** | README §1 fordert "The solution **must be unique**." und §2.1 UC4 fordert "Solutions **must be calculated with an algorithm**." Der Solver ist die zentrale Logik-Komponente — falsche Ergebnisse zerstören UC05, UC07, UC09. | Solver-Unit-Tests gegen die 2 README-Beispiele + Edge-Cases (unlösbar, mehrdeutig). Performance-Boundary < 2s (siehe **AC11.2**). |
| **Q2** | **Lösungs-Eindeutigkeit (Konsistenz beim Speichern)** | README §1 "The solution must be unique." + §2.1 UC5 "The puzzle **can only be saved if it is solvable**." Multi-Solution-Puzzles werden abgelehnt — nicht nur Zero-Solution (siehe **V07**). | Integration-Test: Speichern eines mehrdeutigen Puzzles → kein DB-Row. |
| **Q3** | **Test-Abdeckung & Test-Plan-Termin** | README §3.1 fordert pro UC mind. 1 positiver + 1 negativer + (wo möglich) 1 Boundary-Test. README §1.4: "submission of the planned test cases must take place **by 11:30 o'clock**." | Test-Plan vor 11:30 abgegeben; Unit-Tests in Test-Framework lt. README §3.2 ("**should be implemented with a test framework**"). Coverage-Ziel siehe [Kapitel 10](#chapter-10). |

### Weitere relevante Qualitätsmerkmale (sekundär)

- **Sicherheit** — README §2.1 UC2: "a login **is required**." → Authentifizierung, geschützte Routen, sichere Passwort-Speicherung (siehe **V03**, **V16**).
- **Bedienbarkeit** — README §1.1 fordert "appropriate navigation" + "controls for all functions" → vollständig adressiert in **Mockup-Briefings**.
- **Wartbarkeit** — Klare Trennung Service-Layer / DB-Layer / UI (siehe Service-Interfaces in **Funktionalitäts-Matrix**).

---

## §1.3 Stakeholder

| Rolle | Erwartung | Berührungspunkte |
|-------|-----------|------------------|
| **Wettbewerbs-Prüfer** (Skills Battle Jury) | Konforme Submission gemäss README §1.4 — ZIP-File, Doku als PDF, Source-Code, Executables, DB-Script. Bewertung gegen UCs und Test-Protokoll. | `AppDev_Name_FirstName.zip` (siehe [Kapitel 2 §2.2](#chapter-2)). |
| **Wettbewerber** (Tim Fischer, Implementierer) | Zeit-effiziente, testbare Architektur. Strikte Vermeidung von Scope-Creep. Reproduzierbares lokales Setup. | Source-Code, Test-Suite, lokaler Build mit `dotnet`. |
| **End-Nutzer** (Spieler der App) | Funktionierender Killer-Sudoku-Spielfluss: Account → Puzzle wählen → spielen → Hint → Lösung prüfen → Highscore. UI-Sprache deutsch. | UI-Screens S1–S7 (**Mockup-Briefings**). |
| **Puzzle-Ersteller** (User-Subgruppe, kreiert Puzzles via UC04/UC05) | Verlässliche Solvability-Prüfung, kein Datenverlust bei Reject. | Screen S5 — EnterPuzzle (**Mockup-Briefings**). |

### Nicht-Stakeholder (bewusst ausgegrenzt)

Folgende Rollen sind im Scope **nicht** vorgesehen (Begründung: nicht in README spezifiziert, keine UC-Anker):

- Admin-Rolle / Moderation
- Externe Systeme (Social-Login, Payment, Analytics)
- Mehrspieler-/Live-Funktionen

---

## Verweise

- Use Cases UC01–UC14
- Validation Rules V01–V16
- **Funktionalitäts-Matrix**
- [Kapitel 2 — Randbedingungen](#chapter-2)
- [Kapitel 3 — Kontextabgrenzung](#chapter-3)
