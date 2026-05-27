<h1 id="chapter-4">4 Lösungsstrategie</h1>

> arc42 v8.2 · Kapitel 4 · Killer Sudoku
> Bezug: [`00-README.md`](#toc) (Index) · **Use-Cases-Dokument** · **Funktionalitäts-Matrix** · **Validation-Regeln** · **ER-Modell** · **Aufgabenstellung**

Dieses Kapitel fasst die fundamentalen Entscheidungen zusammen, die alle nachfolgenden Kapitel tragen. Die Aufgabenstellung gibt **kein** spezifisches Framework vor (siehe §1.3 "free to build the database schema", §1 "your task is to plan, implement, and test"). Damit sind Technologie-Wahl und Top-Level-Zerlegung explizit Architektur-Entscheidungen — sie sind hier dokumentiert.

---

## 4.1 Technologie-Entscheidungen

Die folgende Tabelle ordnet jede zentrale Anforderung aus dem Aufgaben-README einer technischen Lösung zu. Vollständige Begründung in [Kapitel 9 — Architekturentscheidungen](#chapter-9) (TBD).

| Anforderung (Quelle) | Gewählte Lösung | Begründung |
|----------------------|-----------------|------------|
| "login is required" (UC02/UC03) | **ASP.NET Core Identity** (Cookie-Auth) | Built-in für .NET 10, PBKDF2-Hashing, CSRF/XSRF-Token frei Haus, deckt **V03**/**V15** Defense-in-Depth |
| "stored in the database" (UC04) | **Microsoft SQL Server Express** + Entity Framework Core 10 | README erlaubt explizit MS-SQL oder MySQL; MS-SQL Express ist lokal lizenzkostenfrei, native T-SQL-Constraints (CHECK/UNIQUE/Filtered Index/Trigger) tragen DB-Layer-Verteidigung aus **ER-Modell** |
| "must be calculated with an algorithm" (UC04/UC11) | **Reiner C#-Solver** (`ISolverService`) als isolierter Domain-Kern ohne Framework-Abhängigkeit | Pure Logik → Unit-testbar ohne DB/UI; Backtracking + Constraint-Propagation; siehe **Funktionalitäts-Matrix** Service-Block |
| "solution must be unique" (§1) | Solver bricht nach dem **zweiten** gefundenen Pfad ab → `Solutions ∈ {0,1,2}` (**V07**) | Performance-Begrenzung (AC11.2: <2 s); 2 ist Marker für "≥2" |
| Interaktives Grid (UC04, UC06, UC07, UC14) | **Blazor Server** (Razor Components + SignalR) | Server-rendered: Solver/Validierung läuft serverseitig (kein clientseitiges Cheaten möglich), `[Authorize]`-Attribut für Routen, kein zweites SPA-Frontend-Projekt nötig |
| "test cases ... unit tests ... test framework" (§3) | **xUnit** + **bUnit** (Component-Tests) + **EF Core InMemory / Testcontainers** | Standard für .NET; bUnit erlaubt Blazor-Komponenten-Tests ohne Browser; siehe **Test-Protokoll** |
| Validation-Defense (V01–V16) | **3 Layer**: Client (Blazor `EditForm`/Annotations) → Server (`IXxxService`) → DB (CHECK/UNIQUE/Trigger) | "Defense in Depth" aus **Validation-Regeln** Header — kritische Regeln auf mind. 2 Layern |
| Sum-Check "405" (§2.3) | **Fast-Fail in `IGameService.CheckSolutionAsync`** vor Voll-Algorithmus (**V09**) | README §2.3: "Use the value for a simple validation before checking the solution with an algorithm" — wörtlich umgesetzt |

> **Hinweis:** Die Solver-Implementierung wird über das Interface `ISolverService` aus **Funktionalitäts-Matrix** entkoppelt — der konkrete Backtracker ist eine austauschbare Detail-Entscheidung, kein Architektur-Pfeiler.

---

## 4.2 Top-Level-Zerlegung

Das System wird in eine **klassische 3-Layer-Architektur** zerlegt, ergänzt um einen **isolierten Domain-Kern** für die rechenintensive Solver-Logik. Diese Zerlegung erscheint im Detail in [Kapitel 5 — Bausteinsicht](#chapter-5).

```mermaid
flowchart TB
 subgraph Presentation["Presentation Layer<br/>KillerSudoku.Web"]
 Pages["Blazor Pages<br/>(Home, Login, Register,<br/>Puzzles, EnterPuzzle,<br/>PlayPuzzle, Highscore)"]
 Components["Blazor Components<br/>(PuzzleGrid, CageEditor,<br/>HintButton, Toolbar, ...)"]
 Identity["ASP.NET Identity<br/>SignInManager · UserManager"]
 end

 subgraph Application["Application Layer<br/>(KillerSudoku.Data/Services)"]
 PuzzleSvc["IPuzzleService"]
 GameSvc["IGameService"]
 HintSvc["IHintService"]
 HighSvc["IHighscoreService"]
 end

 subgraph Domain["Domain Kern (isoliert)<br/>KillerSudoku.Core (Solver)"]
 Solver["ISolverService<br/>(Backtracking +<br/>Constraint Propagation)"]
 Validator["Validator<br/>(Row/Col/Nonet/Cage-Check)"]
 end

 subgraph Data["Data Layer<br/>KillerSudoku.Data"]
 EF["SudokuDbContext<br/>(EF Core 10)"]
 Views["vw_Highscore"]
 end

 DB[("MS-SQL Express<br/>sudoku DB")]

 Pages --> Components
 Components -->|"DI-Call"| AuthSvc
 Components -->|"DI-Call"| PuzzleSvc
 Components -->|"DI-Call"| GameSvc
 Components -->|"DI-Call"| HintSvc
 Components -->|"DI-Call"| HighSvc

 PuzzleSvc --> Solver
 GameSvc --> Validator
 HintSvc --> Solver

 AuthSvc --> EF
 PuzzleSvc --> EF
 GameSvc --> EF
 HighSvc --> Views

 EF --> DB
 Views --> DB
```

**Schichten-Verantwortung:**

| Schicht | Verantwortung | Abhängigkeit nach |
|---------|---------------|--------------------|
| **Presentation** (`KillerSudoku.Web`) | UI-Rendering, Routing, User-Interaktion, Input-Binding, Display-State | nur **Application** (über Interfaces) |
| **Application** (`KillerSudoku.Core` Services) | Use-Case-Orchestrierung, Transaktions-Grenzen, Autorisierung, Aufruf des Solvers | **Domain** + **Data** |
| **Domain-Kern** (`KillerSudoku.Core` Solver) | Reine Algorithmik (Backtracking, Constraint-Propagation, Sum-Check) | **keine** (pure C# — kein EF, kein ASP.NET) |
| **Data** (`KillerSudoku.Data`) | EF-Core-Mapping, Migrations, Query-Performance, View-Zugriff | DB-Treiber |

**Der Solver als isolierter Domain-Kern** ist die wichtigste Designtrennung: Er ist die kritische Geschäftslogik (UC11 wird von UC05, UC07, UC09 verwendet — siehe Abhängigkeits-Graph in **Funktionalitäts-Matrix** §Kritische Abhängigkeiten) und muss separat per Unit-Test verifiziert werden. Pure C# ohne Framework-Bindung erlaubt Tests in Millisekunden statt Sekunden.

---

## 4.3 Erreichen der Qualitätsanforderungen

Die wichtigsten Qualitätsziele werden durch konkrete Patterns adressiert. Vollständige Liste in [Kapitel 10](#chapter-10) (TBD); hier nur die strategie-prägenden Mappings.

| Qualitätsziel | Pattern / Mechanismus | Bezug |
|---------------|----------------------|------|
| **Korrektheit** — Sudoku- und Killer-Regeln werden niemals verletzt | (a) DB-Constraints als letzte Verteidigung (**V05**, **V06**, **V08**). (b) Server-seitige Re-Validierung in `IGameService.CheckSolutionAsync` (UC09). (c) Solver muss "solution must be unique" garantieren (**V07**). | README §1 Cage-Regeln |
| **Performance** — Solver < 2 s, UI-Reaktion < 200 ms | (a) Sum-Check 405 als Fast-Fail vor Algorithmus (AC09.1). (b) Solver bricht bei der 2. Lösung ab (AC11.2). (c) Paginierte Puzzle-Listen (AC12.2). (d) Highscore-Read derzeit als LINQ-Join im `HighscoreService` (View `vw_Highscore` existiert im Schema als Reserve, wird aktuell nicht gemappt — siehe **ER-Modell**). | UC11 AC11.2 |
| **Testbarkeit** — 80 %+ Coverage, TDD-fähig | (a) Service-Interfaces (`I*Service`) erlauben Mock-Substitution. (b) Solver als pure Funktion testbar ohne DB. (c) bUnit für Component-Tests, EF InMemory für Integration. | **Test-Protokoll** |
| **Sicherheit** — kein Klartext-Passwort, kein XSS, kein CSRF | (a) ASP.NET Identity PBKDF2 (**V03**). (b) Razor `@`-Encoding (**V14**). (c) Antiforgery-Token in EditForm (**V15**). (d) `[Authorize]` + UserId-Ownership (**V16**). | UC02/UC10/UC13 |
| **Wartbarkeit** — kleine, fokussierte Files | "Many small files > few large files" aus globalen Code-Standards: 200–400 Zeilen pro Komponente, Service oder Repo. Komponenten-Tree spiegelt Domain-Begriffe. | Coding-Style-Regel |
| **Datenintegrität** — keine inkonsistenten Game-States | (a) Filtered Unique Index `UX_Game_ActiveOnly` (max 1 aktives Game pro User-Puzzle). (b) Trigger `trg_CageCell_UniquePerPuzzle` (Cell-Cage-Eindeutigkeit). (c) Transaktion in `SaveIfSolvableAsync` (Puzzle + Cage + CageCell atomar). | **ER-Modell** Constraints |

---

## 4.4 Organisatorische Strategie

Die Aufgabenstellung schreibt einen **klaren zeitlichen Ablauf** vor (§3.1: "Submission der geplanten Test-Cases bis 12 Uhr", §1.4 "Doku + Source + DB-Script"). Das diktiert die Reihenfolge der Artefakte:

```mermaid
flowchart LR
 A[Spec lesen<br/>README §1-3] --> B[Use-Cases<br/>+ AC formulieren]
 B --> C[Validation-Regeln<br/>V01-V16]
 C --> D[Test-Cases<br/>planen]
 D -->|12:00 Submission| E[(Test-Protokoll<br/>geliefert)]
 D --> F[Mockups<br/>+ ERM + SQL]
 F --> G[Architektur<br/>arc42 Kapitel 1-12]
 G --> H[Implementation<br/>TDD: Red-Green-Refactor]
 H --> I[Final Submission<br/>Zip]
```

**Tragende organisatorische Prinzipien:**

1. **Spec → Tests → Code** (TDD): Jeder Test-Case in **Test-Protokoll** verlinkt auf eine `AC*`-ID; jede AC referenziert wörtlich den README-Text via "Source"-Zeile.
2. **Solver zuerst**: Da `ISolverService` Pflicht-Dependency für UC05, UC07, UC09 ist (siehe **Funktionalitäts-Matrix** §Kritische Abhängigkeiten), wird er als erstes implementiert und mit Beispielen aus den 2 README-Examples (§1.2) sowie Edge-Cases (unsolvable, multi-solution) abgesichert.
3. **Validation auf 3 Layern**: Jede strikte Constraint wird auf Client + Server + DB redundant gesichert. Dies macht Tests gegen einzelne Layer wertvoll, statt Doppel-Tests.
4. **Mockup vor Implementierung**: README §1.1 verlangt Mockups als Doku-Bestandteil — sie werden separat erstellt (**Mockup-Briefings**) und sind die UI-Spezifikation, gegen die Blazor-Components gebaut werden.
5. **arc42 referenziert, dupliziert nicht**: Die Quell-Dokumente (**Use-Cases-Dokument**, **Funktionalitäts-Matrix**, **Validation-Regeln**, **ER-Modell**) sind die Single Source of Truth — arc42-Kapitel verlinken sie statt sie zu kopieren. So gibt es keine driftenden Mehrfach-Quellen.

---

> **Nächste Kapitel:**
> - [Kapitel 5 — Bausteinsicht](#chapter-5): Detail der drei Layer mit Klassen, Schnittstellen, Verantwortungen.
> - [Kapitel 6 — Laufzeitsicht](#chapter-6): Vier Schlüssel-Szenarien (Puzzle-Save, Hint, Check, Pause/Resume) als Sequenz-Diagramme.
