<h1 id="chapter-8">8 Querschnittliche Konzepte</h1>

> arc42 v8.2 · Skills Battle 2026 — Application Development — Killer Sudoku
> Quelldokument (autoritativ): **Aufgabenstellung**

Dieses Kapitel sammelt Konzepte, die mehrere Bausteine oder Schichten der Architektur betreffen. Jedes Konzept verweist zurück auf seine UC- und Validation-Anker; eigentliche Regel-Listen werden nicht dupliziert, sondern referenziert.

---

## §8.1 Authentifizierung & Autorisierung

### Konzept

Die Anwendung verwendet **ASP.NET Core Identity** als Authentifizierungs-Provider und Cookie-basierte Sessions. Login ist Pflicht für alle nicht-öffentlichen Use Cases (UC04–UC14). UC01 (Read Rules) ist die einzige öffentliche Seite.

### Anforderungen aus README

- README §2.1 UC2: "To use the application, a login **is required**."
- README §2.1 UC3: "The user logs in."

### Implementations-Konzept

| Aspekt | Lösung |
|--------|--------|
| Identity-Provider | ASP.NET Core Identity (Standard `IdentityUser`-Schema, lokale Tabelle `AppUser`) |
| Passwort-Hashing | ASP.NET Identity Default (PBKDF2 mit HMAC-SHA256, 100 000 Iterationen) — siehe **V03** |
| Session-Mechanismus | Cookie (HttpOnly + Secure + SameSite=Lax — siehe §8.9) |
| Rate-Limiting Login | 5 Fehlversuche / 5 Minuten — siehe **V04** |
| Geschützte Routen | `[Authorize]`-Attribut auf Razor-Pages bzw. Components — siehe **V16** |
| Cross-User-Mutation | Server prüft `game.UserId == currentUserId` vor jeder schreibenden Aktion — siehe **V16** |

### Validation-Anker

- V03 — Passwort
- V04 — Login Rate-Limit
- V16 — Authorization

### Use-Case-Anker

- UC02 — Create User
- UC03 — Login

---

## §8.2 Eingabe-Validierung (Defense in Depth)

### Konzept

Alle User-Inputs werden auf **mindestens zwei Layern** geprüft:

1. **Client** (Blazor `EditForm` + Data Annotations + JS-Pattern)
2. **Server** (Service-Layer)
3. **Datenbank** (CHECK / UNIQUE / FK / Trigger — letzte Verteidigungslinie)

Diese Schichtung folgt der "Defense in Depth"-Konvention aus **Validation-Regeln**:

> "Defense in Depth: Kritische Regeln werden auf mindestens 2 Layern geprüft. DB-Constraints sind die letzte Verteidigungslinie."

### Übersicht

Die vollständige Regel-Liste mit allen 16 Regeln (V01–V16) ist in **Validation-Regeln** dokumentiert. Hier nur die Layer-Zuordnungs-Heuristik:

| Regel-Kategorie | Client | Server | DB |
|-----------------|:------:|:------:|:--:|
| Format (Username, Email, Pattern) | ✓ | ✓ | partiell (UNIQUE, LIKE) |
| Range (Difficulty, CellValue, MarkValue) | ✓ | ✓ | ✓ (CHECK) |
| Solvability (UC05) | — | ✓ (Solver) | — |
| Auth & Authorization | — | ✓ | partiell (FK) |
| XSS-Schutz | — | ✓ (Razor-Auto-Encoding) | — |
| CSRF-Schutz | — | ✓ (Antiforgery) | — |

### Validation-Anker

Vollständige Liste aller Regeln: **Validation-Regeln** V01–V16.

---

## §8.3 Fehlerbehandlung

### Konzept

Fehler werden auf zwei semantische Klassen aufgeteilt:

1. **Erwartete Fachfehler** (z.B. Username vergeben, Puzzle unlösbar, falsche Credentials) → strukturierte `Result<T>`- bzw. Either-Typen
2. **Unerwartete System-Fehler** (DB-Down, NullRef, etc.) → ASP.NET-Exception-Middleware → generische Fehlerseite

### Strukturierte Result-Types

Service-Methoden geben statt Exceptions strukturierte Result-Typen zurück (siehe Service-Signaturen in **Funktionalitäts-Matrix**):

| Result-Type | Verwendet von | Quelle |
|-------------|---------------|--------|
| `IdentityResult` | `UserManager<AppUser>.CreateAsync` | UC02 (ASP.NET-Identity-Standard-Result) |
| `SignInResult` | `SignInManager<AppUser>.PasswordSignInAsync` | UC03 (ASP.NET-Identity-Standard-Result) |
| `ValidationResult` | `IPuzzleService.ValidateStructureAsync` | UC04 |
| `SavePuzzleResult` | `IPuzzleService.SaveIfSolvableAsync` | UC05 |
| `CheckResult` | `IGameService.CheckSolutionAsync` | UC09 |
| `HintResult` | `IHintService.GetHintAsync` | UC07 |
| `SolveResult` | `ISolverService.Solve` | UC11 |

### Generische Login-Fehlermeldung

Bei falschen Credentials liefert UC03 absichtlich **keine** Auskunft darüber, ob der Username existiert (Security-Pattern gegen Account-Enumeration):

> "Username oder Passwort falsch."

Siehe **V03** und UC03 AC03.1:
> "AC03.1: Falsche Credentials → keine Auskunft darüber, ob Username existiert"

### Use-Case-Anker

Alternative Pfade (`Alt`) sind pro UC in **Use-Cases-Dokument** dokumentiert.

---

## §8.4 Logging

### Konzept

Logging via **`Microsoft.Extensions.Logging`** (Standard-Abstraktion in .NET 10). Konkreter Provider: Console + File (Debug-Builds) / Console + EventLog (Release auf Windows).

### Was geloggt wird

| Ereignis | Log-Level | Begründung |
|----------|-----------|------------|
| Anwendungs-Start / Shutdown | Information | Operative Sichtbarkeit |
| Fehlgeschlagene Login-Versuche | Warning | Rate-Limit-Anker (siehe **V04**) |
| Solver-Aufrufe mit Dauer > 1s | Warning | Performance-Boundary aus UC11 AC11.2 ("< 2 Sekunden") |
| Puzzle-Save (Solvability-Check Ergebnis: 0/1/≥2) | Information | Audit für UC05 |
| Unbehandelte Exceptions | Error | Standard |
| HTTP-Requests (Method, Path, Status, Duration) | Information | ASP.NET-Built-In Request-Logging |

### Was NICHT geloggt wird (kritisch)

- **Passwörter** — weder Klartext noch Hash, weder in Request-Bodies noch in Trace-Logs (siehe **V03**: "NIE im Klartext loggen/serialisieren")
- **Session-Cookies / Antiforgery-Token** — kein Logging von Request-Headers oberhalb von Debug-Level
- **PII über Username/Email hinaus** — UC erfordert keine weitergehenden Daten (siehe ERD in **ER-Modell**)

### Validation-Anker

- **V03**

---

## §8.5 Solver-Domänen-Konzept

### Konzept

Der Solver (UC11) ist die zentrale Logik-Komponente und das **erste Qualitätsziel** der Architektur (siehe [Kapitel 1 §1.2 Q1](#chapter-1)). Er wird daher als **Pure Function** im Domain-Layer implementiert:

- **Keine** Abhängigkeit auf `DbContext` oder IO
- **Keine** Abhängigkeit auf ASP.NET-Infrastruktur
- Eingabe: `int[,] givenValues` + `IReadOnlyList<CageDef> cages`
- Ausgabe: `SolveResult { Solutions, Solution? }`

### Algorithmik

Aus **UC11**:

> "Solver implementiert Backtracking mit Constraint-Propagation:
> - Standard-Sudoku-Constraints (Row/Col/Nonet)
> - Killer-Cage-Constraints (Summe + keine Duplikate in Cage)"

Der Solver bricht nach der **zweiten** gefundenen Lösung ab — siehe [ADR-010](#chapter-9).

### Aufrufer

| Aufrufer | UC | Verwendung |
|----------|----|----|
| `IPuzzleService.SaveIfSolvableAsync` | UC05 | `CountSolutions == 1` ⇒ Puzzle wird gespeichert |
| `IHintService.GetHintAsync` | UC07 | Solver liefert Solution; Hint-Strategie wählt Zelle (siehe [ADR-013](#chapter-9)) |
| `IGameService.CheckSolutionAsync` | UC09 | Optional: Solver bestätigt User-Eingabe vs. eindeutige Lösung |

### Testbarkeit

Da der Solver keine externen Dependencies hat, ist er als **reiner Unit-Test** prüfbar:

- xUnit-Tests gegen die 2 README-Beispiele (siehe README §1.2: "You get 2 examples to test your application.")
- Edge-Cases: unlösbar, mehrdeutig, Single-Cell-Cage (Sum=N erzwingt Wert N)
- Performance-Boundary: < 2 Sekunden (UC11 AC11.2)

### Use-Case-Anker

- UC11 — Auto Solve
- [ADR-010](#chapter-9)
- [ADR-013](#chapter-9)

---

## §8.6 Datenzugriff

### Konzept

Datenzugriff über **Entity Framework Core 10** mit eigenem `SudokuDbContext`. Das Schema wird über **EF-Migrations** versioniert (Source of Truth liegt in `KillerSudoku.Data/Persistence/Migrations/`). Aus den Migrations wird das von README §1.3 geforderte deklarative Submission-Script **Datenbank-Skript** per `dotnet ef migrations script` exportiert — beides bleibt konsistent.

### DbContext-Scope

| Aspekt | Lösung |
|--------|--------|
| Lebenszyklus | Scoped pro Blazor-Server-Circuit (eine Connection pro Tab/Session) |
| Connection-String | Aus `appsettings.json` (siehe [Kapitel 7 §7.3](#chapter-7)) |
| Tracking | Default `AsTracking` für UPDATEs (GameCell), `AsNoTracking` für Read-Only-Queries (Puzzle-Liste, Highscore) |
| Transaktionen | Pro Service-Call eine Transaction wenn mehrere Tabellen geschrieben werden (z.B. UC05: Puzzle + Cage + CageCell) |

### Migrations

> Migration-Strategie: **EF-Migrations als Source of Truth**, **Datenbank-Skript** als generiertes Submission-Artefakt.

Workflow:

1. Schema-Änderung in den C#-Entities oder im `SudokuDbContext` → `dotnet ef migrations add <Name>` erzeugt eine neue Migration unter `KillerSudoku.Data/Persistence/Migrations/`.
2. Beim Container-Start (oder via `dotnet ef database update`) werden alle ausstehenden Migrations angewendet.
3. Das deklarative Submission-Skript **Datenbank-Skript** wird per `dotnet ef migrations script --idempotent -o db/sudoku.sql` exportiert — damit hat der Prüfer ein eigenständiges, idempotentes Skript zum manuellen Ausführen (README §1.3).

Begründung:

- EF-Migrations geben versioniertes, reversibles Schema-Management (`dotnet ef migrations remove` / `database update <vorherige>`)
- Der Export liefert genau das von §1.3 geforderte **Datenbank-Skript** ohne manuelle Pflege zweier Wahrheits-Sources
- Idempotenz-Modus (`--idempotent`) macht das Skript wiederholt ausführbar für SSMS / `sqlcmd`

### EF-Modell-Mapping

| C#-Klasse | Tabelle | Bemerkung |
|-----------|---------|-----------|
| `AppUser` | `AppUser` | Spalten-Name aus T-SQL-Reserved-Konflikt — siehe [ADR-005](#chapter-9) |
| `Puzzle` | `Puzzle` | KEINE Solution-Spalte (siehe [ADR-007](#chapter-9)) |
| `Cage` | `Cage` | mit `[Sum]`-Property (T-SQL-Reserved) |
| `CageCell` | `CageCell` | `RowIdx`/`ColIdx` statt `Row`/`Col` — siehe [ADR-006](#chapter-9) |
| `Game` | `Game` | Pause-Felder siehe [ADR-009](#chapter-9) |
| `GameCell` | `GameCell` | `CellValue` NULL-fähig |
| `PencilMark` | `PencilMark` | eigene Tabelle, kein JSON — siehe [ADR-008](#chapter-9) |
| `HintLog` | `HintLog` | Audit-Log |

### Highscore-View

Highscore ist eine **DB-View** `vw_Highscore` (siehe **Datenbank-Skript** und [ADR-003](#chapter-9)). EF mappt sie als read-only Keyless-Entity. Der Service `IHighscoreService.GetTopAsync` selektiert nur aus dieser View und sortiert nach `Score DESC`.

### Anker

- **ER-Modell** — vollständiges ERD mit Spalten-Typen
- **Datenbank-Skript** — Schema
- **Funktionalitäts-Matrix** — Service × DB-Operations-Matrix

---

## §8.7 UI-Komponenten

### Konzept

Die UI ist in einer **Blazor-Component-Hierarchie** organisiert. Pro Screen (S1–S7) existiert eine Root-Component (`@page`-Route), die kleinere Children komponiert. Komponenten sind nach **Single Responsibility** geschnitten und einzeln via bUnit testbar.

### Component-Hierarchie

Übersicht aus **Funktionalitäts-Matrix** (Spalte "Blazor-Component"):

```
Layout.razor
├── Home.razor (S1, public, UC01)
│ ├── RulesPanel.razor
│ └── MiniSudokuExample.razor
├── Register.razor (S2, public, UC02)
│ └── RegisterForm.razor
├── Login.razor (S3, public, UC03)
│ └── LoginForm.razor
├── Puzzles.razor (S4, [Authorize], UC12)
│ ├── FilterBar.razor
│ └── PuzzleCard.razor
├── EnterPuzzle.razor (S5, [Authorize], UC04+UC05)
│ ├── PuzzleGrid.razor
│ └── CageEditor.razor
├── PlayPuzzle.razor (S6, [Authorize], UC06-UC09, UC13, UC14)
│ ├── PuzzleGrid.razor
│ │ └── PencilMarkLayer.razor (UC14)
│ ├── Toolbar.razor
│ │ ├── HintButton.razor (UC07)
│ │ ├── PauseButton.razor (UC13)
│ │ └── CheckSolutionButton.razor (UC09)
└── Highscore.razor (S7, [Authorize], UC08)
 └── HighscoreTable.razor
```

### Quell-Anker

- Screen-Inventar: **Funktionalitäts-Matrix**
- Component × UC × Service × DB: **Funktionalitäts-Matrix**
- Visuelle Briefings: **Mockup-Briefings**

### Component-Kommunikation

| Pattern | Verwendung |
|---------|------------|
| `[Parameter]` | Eltern → Kind (Down) |
| `EventCallback<T>` | Kind → Eltern (Up) |
| `CascadingValue` | Spielzustand in `PlayPuzzle`-Subtree |
| Service-Injection via DI | Components → Service-Layer |

### State-Management

Spielzustand (Grid, Pencil Marks, Pause-Status) lebt auf dem Server pro Blazor-Circuit. Bei Disconnect/Reconnect lädt der Component-Init aus `IGameService` den persistierten Zustand aus der DB nach (Auto-Save bei jedem Move — siehe **UC13** "Browser-Tab schliessen ohne Pause → State wird trotzdem persistiert").

---

## §8.8 Internationalisierung

### Konzept

**UI-Sprache: Deutsch** (einsprachig). Es ist keine Internationalisierung (i18n) implementiert.

### Begründung

- Das README spezifiziert keine i18n-Pflicht (kein "must" oder "required" zu Sprachen)
- Der Wettbewerb ist im deutschsprachigen Raum verortet (Skills Battle)
- Die Stakeholder-Tabelle in [Kapitel 1 §1.3](#chapter-1) hält fest: "UI-Sprache deutsch."

### Umsetzung

- Alle UI-Strings sind deutsch hardkodiert in Razor-Markup oder als `const string` (kein `IStringLocalizer`)
- Fehlermeldungen sind deutsch (Beispiele aus **Validation-Regeln**):
 - "Schwierigkeit muss 1, 2 oder 3 sein."
 - "Username oder Passwort falsch."
 - "Puzzle ist nicht lösbar."
- Datums-/Zeit-Formate: `de-CH` Culture (kann via `appsettings.json` umgestellt werden, ist aber kein Pflicht-Feature)

> Falls i18n nachträglich gefordert wird, ist die Migration unproblematisch (`.resx`-Files + `@inject IStringLocalizer`). Aktuell aus Scope ausgeschlossen.

---

## §8.9 Sicherheit

### Konzept

Sicherheit ist als **Defense in Depth** über mehrere Layer organisiert. Die hier aufgeführten Massnahmen leiten sich direkt aus den README-Vorgaben (Login Pflicht, Solution-Eindeutigkeit) und den Validation-Regeln V01–V16 ab.

### Massnahmen-Matrix

| Bedrohung | Massnahme | Layer | Anker |
|-----------|-----------|-------|-------|
| Account-Diebstahl (Replay/Theft) | Auth-Cookie: `HttpOnly` + `Secure` + `SameSite=Lax` | Server (Identity-Konfiguration) | **V15**, UC03 AC03.3 |
| Cross-Site Request Forgery | ASP.NET Antiforgery Token (Standard in Blazor Server `EditForm`) | Server | **V15** |
| Cross-Site Scripting (XSS) | Razor `@expression` → automatisches HTML-Encoding; **kein** `@((MarkupString))` für User-Content | Server | **V14** |
| SQL-Injection | EF Core Parameterized Queries (keine String-Konkatenation in SQL) | Server | implicit (EF Core Default) |
| Passwort-Klartext-Leak | PBKDF2-Hash via ASP.NET Identity; **NIE** Klartext loggen | Server | **V03**, §8.4 |
| Account-Enumeration | Generische Login-Fehlermeldung ("Username oder Passwort falsch") | Server | **V03**, UC03 AC03.1 |
| Brute-Force-Login | Rate-Limit: 5 Versuche / 5 Minuten | Server | **V04** |
| Cross-User-Mutation | Server-side Check `game.UserId == currentUserId` vor jedem Write | Server | **V16** |
| Unauthorized Routes | `[Authorize]`-Attribut + Redirect zu `/login` | Server | **V16** |
| Puzzle-Manipulation (Hash-Probleme bei Solve) | Solver-Logik ist serverseitig, kein Trust auf Client-Input | Server | **UC09** |

### Cookie-Konfiguration

```csharp
services.ConfigureApplicationCookie(options =>
{
 options.Cookie.HttpOnly = true;
 options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
 options.Cookie.SameSite = SameSiteMode.Lax;
 options.ExpireTimeSpan = TimeSpan.FromHours(2);
 options.LoginPath = "/login";
 options.AccessDeniedPath = "/access-denied";
});
```

> **Cookie-Lifetime 2 h fix** (kein SlidingExpiration): minimiert das Theft-Window bei kompromittiertem Cookie. Für eine Login-Required-App ohne sensitive Long-Running-Sessions die sicherere Wahl gegenüber 24 h mit Sliding-Refresh.
> **SameSite=Lax** (statt `Strict`) erlaubt Top-Level-Navigation mit Cookie (z.B. Klick auf externen Link, der zurück führt). `Lax` ist OWASP-Recommendation für Web-Apps mit normalem Login-Flow.

## Verweise

- **Use-Cases-Dokument** — UC01–UC14
- **Validation-Regeln** — V01–V16
- **Funktionalitäts-Matrix** — Service- & UI-Inventar
- **ER-Modell** — DB-Modell & Design-Entscheidungen
- [Kapitel 7 — Verteilungssicht](#chapter-7)
- [Kapitel 9 — Architekturentscheidungen](#chapter-9)
