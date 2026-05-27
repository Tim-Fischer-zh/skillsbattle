<h1 id="chapter-2">2 Randbedingungen</h1>

> arc42 v8.2 · Killer Sudoku App
> Quelle für Pflicht-Vorgaben: **Aufgabenstellung**

Randbedingungen sind **nicht-verhandelbare** Vorgaben, die das Design und die Implementierung einschränken. Sie sind in technische, organisatorische und Konventions-Constraints gegliedert.

---

## §2.1 Technische Randbedingungen

### Plattform & Stack

| Constraint | Wert | Quelle / Begründung |
|------------|------|---------------------|
| **Runtime / Framework** | **.NET 10** | Wettbewerbs-Stack-Festlegung Tim Fischer. Dokumentiert in **Use-Cases-Dokument** (Stack-Header). |
| **UI-Technologie** | **Blazor Server** | Server-Side Rendering mit SignalR-Circuit; spec-konform für interaktive Web-Anwendung lt. README §1.1 (Designfreiheit). |
| **Datenbank** | **MS-SQL Server Express** | README §1.3 erlaubt "MySQL or MS-SQL server running on your machine." Entscheidung: MS-SQL Express. DB-Name strikt: **`sudoku`** (README §1.3: "create a database named **'sudoku'**"). |
| **ORM / Data-Access** | Entity Framework Core 10 (oder Dapper) | Wahl noch offen; siehe **ER-Modell**. Beeinflusst Service-Interfaces nicht. |
| **Authentication** | ASP.NET Core Identity (Cookie-basiert) | Aus README §2.1 UC2 ("a login **is required**") abgeleitet; siehe **V03** + **V16**. |

### Test-Frameworks

README §3.2 (wörtlich): "Running the test cases **should be implemented with a test framework** and is part of the submission (e.g. JUnit in Java)." Für .NET ergibt sich:

| Test-Typ | Framework | Zweck |
|----------|-----------|-------|
| **Unit-Tests** | **xUnit** | Service-Layer, Solver, Validation-Logic (Q1, Q3 — siehe [§1.2](#chapter-1)). |
| **Component-Tests** | **bUnit** | Blazor-Komponenten (Grid, CageEditor, Toolbar). |
| **E2E / UI-Tests** | **Playwright (.NET)** | Smoke-Tests kritischer Flows. README §3.2 erlaubt "UI tests can be run **without** a test framework" — Playwright ist optionale Erweiterung. |

### Datenbank-Setup (README §1.3, wörtlich zitiert)

- **Database-Name:** "sudoku" (Pflicht-Name aus README §1.3)
- **Schema-Freiheit:** "You are free to build the database schema."
- **SQL-Script-Pflicht:** "Create for each table a SQL statement and save them in a script called ****Datenbank-Skript****."
- **Constraints-Pflicht:** "Don't forget to create the primary and foreign key constraints."

Das Schema und die Constraints sind in **ER-Modell** modelliert; das SQL-Script wird in `db/sudoku.sql` ausgeliefert.

### Browser-Support

| Browser | Status | Begründung |
|---------|--------|------------|
| **Chrome** (current stable) | Pflicht-Target | E2E-Tests laufen primär in Chromium-Engine. |
| **Firefox** (current stable) | Sekundär-Target | Manuelle Smoke-Verifikation. |
| Safari / Edge | Best-effort | Nicht im README spezifiziert, keine Test-Verpflichtung. |

### Entwicklungsumgebung (IDE-Auswahl)

| IDE | Status |
|-----|--------|
| **Visual Studio 2022/2026** | Primär (Blazor-Server-Templates, MS-SQL-Tools) |
| **Visual Studio Code** + C# Dev Kit | Alternative |
| **JetBrains Rider** | Alternative |

Die Wahl ist intern; das Build- und Test-Setup muss in **allen drei** funktionieren (kein IDE-gebundenes Projekt-Setup).

> **UNCLEAR:** Eine exakte minimale .NET-SDK-Version wird vom README nicht vorgegeben. Annahme: .NET 10 (LTS-Kandidat zum Wettbewerbszeitpunkt).

---

## §2.2 Organisatorische Randbedingungen

### Submission-Termine (README §1.4 + §3.1)

| Termin | Pflicht (wörtliches Zitat) |
|--------|----------------------------|
| **12:00 Uhr** (Wettbewerbstag) | "submission of the planned test cases **must take place by 12 o'clock**" (README §1.4 + §3.1). |
| **Ende Wettbewerbstag** | Komplette Submission (ZIP) gemäss README §1.4. |

### Deliverables (README §1.4, wörtlich)

> "All deliverables must be submitted in a zip file named **`AppDev_Name_FirstName.zip`**."
> "**Only the contents of the zip file will be considered for the evaluation**."

Pflicht-Inhalte der ZIP-Datei:

| Deliverable | Format | README-Zitat |
|-------------|--------|--------------|
| Dokumentation inkl. Test-Protokoll | **PDF** | §2.6: "For the delivery, create a **PDF document**." |
| Executable Files | Build-Artefakte (.exe / `dotnet publish` Output) **+ Container-Image-Tag** (`ghcr.io/tim-fischer-zh/killer-sudoku:latest`) | §1.4: "executable files" |
| Source-Code | Vollständig, inkl. `Dockerfile`, `docker-entrypoint.sh`, `docker-compose.yml`, `.github/workflows/docker.yml` | §1.4: "the source code" |
| DB-Script | **Datenbank-Skript** (im Container automatisch beim ersten Start applied) | §1.3: "save them in a script called sudoku.sql" |

### Container-Variante (zusätzlich zur Native-Variante)

Die Submission liefert ein **Single-Image-Container-Setup** als bevorzugten Startpfad (siehe [ADR-015](#chapter-9) und [§7.5](#chapter-7)). Der Prüfer kann zwischen drei Pfaden wählen:

1. **`docker pull` + `docker run`** — Image direkt von `ghcr.io` (≈ 1.5 GB), App in unter 60 Sek startklar.
2. **`docker compose up --build`** — lokal aus dem Repo-Root bauen.
3. **Native** — MS-SQL Express + .NET 10 SDK lokal, **Datenbank-Skript** manuell deployen (Standard-Pfad, vollständig in [§7.2 / §7.3](#chapter-7) dokumentiert).

README §1.3 ("running on your machine") wird in allen drei Pfaden erfüllt — Container laufen auf der Maschine des Prüfers.

### Dokumentations-Inhalt (README §2.6, wörtlich)

Die Dokumentation muss folgende Sektionen enthalten:

> "Mockup · Database diagram · Class diagram · Additionally chosen use cases · Validation rules · Test protocol"

Diese Punkte werden über die arc42-Kapitel und die in `docs/` referenzierten Spezialdokumente abgedeckt:

| README-Sektion | Quelle im Repo |
|----------------|----------------|
| Mockup | **Mockup-Briefings** + `docs/mockups/*.png` |
| Database diagram | **ER-Modell** |
| Class diagram | [Kapitel 5 — Bausteinsicht](#chapter-5) (TBD) |
| Additionally chosen use cases | **Use-Cases-Dokument** (UC12–UC14) |
| Validation rules | **Validation-Regeln** |
| Test protocol | **Test-Protokoll** (TBD) |

### Bewertungsmaterial-Beschränkung

> README §1.4: "Only the contents of the zip file will be considered for the evaluation."

Konsequenz: Externe Repos, Cloud-Builds oder Online-Demos werden **nicht** in die Bewertung einfliessen. Alles, was zur Bewertung beitragen soll, muss im ZIP enthalten sein.

---

## §2.3 Konventionen

### Sprach-Konvention

| Bereich | Sprache | Begründung |
|---------|---------|------------|
| **UI-Texte** (Buttons, Labels, Fehlermeldungen) | **Deutsch** | Implementierer + Zielgruppe in DACH-Region; konsistent mit Mockup-Briefings (**Mockup-Briefings**) und Validation-Texten (**Validation-Regeln**). |
| **Code-Identifier** (Klassen, Methoden, Variablen) | **Englisch** | .NET-Konvention; siehe Service-Interfaces in **Funktionalitäts-Matrix**. |
| **Architektur-Doku (arc42)** | **Deutsch** | arc42-Standard im DACH-Kontext; README-Zitate bleiben original-englisch in `"..."`. |
| **DB-Schema-Namen** | **Englisch** | Englische Tabellennamen (`AppUser`, `Puzzle`, `Cage`, …); siehe **ER-Modell**. |

### Code-Style

- **C# Style:** Microsoft .NET Coding Conventions (PascalCase für Public, camelCase für lokal, `_camelCase` für private fields).
- **Nullable Reference Types:** aktiviert (`<Nullable>enable</Nullable>` im csproj).
- **Async-Konvention:** I/O-bound Methoden mit `Async`-Suffix und `Task`-Return — sichtbar an Service-Interfaces (z.B. `IPuzzleService.SaveIfSolvableAsync`, siehe **Funktionalitäts-Matrix**).
- **Immutability bevorzugt** wo idiomatisch (DTOs als `record`).

### Sicherheits-Konventionen (aus Validation-Regeln abgeleitet)

| Konvention | Quelle |
|------------|--------|
| **Login required** für alle geschützten Routen (Puzzles, Spielen, Highscore) | README §2.1 UC2 + **V16** |
| **Passwort wird NIE im Klartext gespeichert** | **V03** |
| **Generische Login-Fehlermeldung** (kein User-Existenz-Hinweis) | **V03** + **AC03.1** |
| **CSRF-Token** auf allen POST/Form-Submissions (Blazor-Standard) | **V15** |
| **HTML-Encoding** auf User-Content (`@`-Razor-Syntax) | **V14** |
| **Defense in Depth:** kritische Validierungen auf Client + Server + DB-Constraint | Header **Validation-Regeln** |

## Verweise

- [Kapitel 1 — Einführung und Ziele](#chapter-1)
- [Kapitel 3 — Kontextabgrenzung](#chapter-3)
- Validation Rules
- **ER-Modell**
- **Funktionalitäts-Matrix**
